using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using Mono.Cecil;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;

namespace Vulkan.Binder {
	public class AssemblyResolver : IAssemblyResolver {
		/// <summary>
		/// A helper that returns the result of a <see cref="Func{TResult}"/>,
		/// or in the event of an exception, <paramref name="default"/>.
		/// </summary>
		/// <typeparam name="T">A given result type, the result of <paramref name="fn"/> and the type of <paramref name="default"/>.</typeparam>
		/// <param name="fn">A given function delegate that returns a <typeparamref name="T"/> or throws an exception.</param>
		/// <param name="default">An alternative in the event that an exception is caught while executing <paramref name="fn"/>.</param>
		/// <returns>The result of <paramref name="fn"/> or in the event of an exception, <paramref name="default"/>.</returns>
		internal static T TryOrDefault<T>(Func<T> fn, T @default = default(T)) {
			try {
				return fn();
			}
			catch {
				return @default;
			}
		}

		/// <summary>
		/// A shorthand helper for use with
		/// <see cref="Enumerable.Where{TSource}(System.Collections.Generic.IEnumerable{TSource},System.Func{TSource,bool})"/>.
		/// </summary>
		/// <typeparam name="T">A given class type, nullable.</typeparam>
		/// <param name="instance">A possibly <c>null</c> or instance of <typeparamref name="T"/>.</param>
		/// <returns>Returns <c>true</c> if <paramref name="instance"/> is not <c>null</c>, otherwise <c>false</c>.</returns>
		internal static bool NotNull<T>(T instance) where T : class => instance != null;

		private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();

		/// <summary>
		/// Returns an arbitrary but incremental timestamp measured in milliseconds.
		/// Internally this uses the <see cref="System.Diagnostics.Stopwatch" /> class.
		/// This provides no guarantee of starting time prior to the first call of this method.
		/// That means that time values returned by this method should only be compared to
		/// other time values returned by this method.
		/// </summary>
		/// <returns>An arbitrary timestamp in milliseconds.</returns>
		public static long CreateTimestamp() {
			return Stopwatch.ElapsedMilliseconds;
		}


		public sealed class AssemblyComparer : IComparer<Assembly> {
			public static readonly AssemblyComparer Instance = new AssemblyComparer();

			public int Compare(Assembly x, Assembly y) {
				if (x == null && y == null) return 0;
				if (x == null) return -1;
				if (y == null) return 1;
				if (ReferenceEquals(x, y)) return 0;
				var xHashCode = x.GetHashCode();
				var yHashCode = y.GetHashCode();
				return xHashCode == yHashCode
					? string.Compare(x.FullName, y.FullName, StringComparison.Ordinal)
					: (xHashCode < yHashCode ? -1 : 1);
			}
		}

		/// <summary>
		///		Scans the current process for all loaded <see cref="ProcessModule"/>s
		///		and discovers them as an <see cref="Assembly" /> if they are one.
		/// 
		///		This updates a cache of <see cref="KnownAssemblies" /> when called.
		/// </summary>
		/// <returns>A set of loaded assemblies.</returns>
		public static IImmutableDictionary<string, Assembly> GetLoadedAssemblies() {
			_lastCheckedLoadedAssemblies = CreateTimestamp();
			var procMods = Process.GetCurrentProcess().Modules.OfType<ProcessModule>();

			_knownAssemblies = procMods.Select(mod => TryOrDefault(()
					=> AssemblyLoadContext.GetAssemblyName(mod.FileName)))
				.Where(NotNull).Select(asmName => TryOrDefault(()
					=> AssemblyLoadContext.Default.LoadFromAssemblyName(asmName)))
				.Where(NotNull)
				.GroupBy(asm => asm.GetName().Name)
				.Select(g => new KeyValuePair<string, Assembly>
					(g.Key, g.OrderByDescending(asm => asm.GetName().Version).First()))
				.ToImmutableDictionary();
			return _knownAssemblies;
		}

		public class AssemblyNameComparer : IEqualityComparer<AssemblyName> {
			public static readonly AssemblyNameComparer Instance
				= new AssemblyNameComparer();

			public bool Equals(AssemblyName x, AssemblyName y) {
				return x.Name == y.Name;
			}

			public int GetHashCode(AssemblyName obj) {
				return obj.Name.GetHashCode();
			}
		}

		private static long _lastCheckedLoadedAssemblies;
		private static IImmutableDictionary<string, Assembly> _knownAssemblies;

		/// <summary>
		/// A set of known loaded assemblies.
		/// Accurate to <see cref="KnownAssembliesTimeout"/> milliseconds.
		/// </summary>
		public static IImmutableDictionary<string, Assembly> KnownAssemblies
			=> CreateTimestamp() - _lastCheckedLoadedAssemblies < KnownAssembliesTimeout
				? (_knownAssemblies ?? GetLoadedAssemblies())
				: GetLoadedAssemblies();

		/// <summary>
		/// A minimum freshness in milliseconds of the <see cref="KnownAssemblies"/> set.
		/// </summary>
		public static int KnownAssembliesTimeout = 120000;

		public void Dispose() {
			// nothing yet
		}

		public AssemblyDefinition Resolve(AssemblyNameReference reference) {
			return Resolve(reference, null);
		}

		public AssemblyDefinition Resolve(AssemblyNameReference reference, ReaderParameters parameters) {
			var refName = reference.Name;

			if (parameters != null && parameters.AssemblyResolver == null)
				parameters.AssemblyResolver = this;

			var resolved = ResolveInternal(refName, parameters);

			return resolved;
		}

		private AssemblyDefinition ResolveInternal(string refName, ReaderParameters parameters) {
			if (KnownAssemblies.TryGetValue(refName, out var loadedAsm)) {
				var path = new Uri(loadedAsm.CodeBase).LocalPath;
				if (File.Exists(path))
					return parameters == null
						? AssemblyDefinition.ReadAssembly(path, new ReaderParameters {AssemblyResolver = this})
						: AssemblyDefinition.ReadAssembly(path, parameters);
			}
			try {
				var freslyLoadedAsm = Assembly.Load(new AssemblyName(refName));

				_knownAssemblies = KnownAssemblies
					.Add(freslyLoadedAsm.GetName().Name, freslyLoadedAsm);

				var path = new Uri(freslyLoadedAsm.CodeBase).LocalPath;
				if (!File.Exists(path))
					throw new DllNotFoundException($"Could not locate {refName}");

				return parameters == null
					? AssemblyDefinition.ReadAssembly(path)
					: AssemblyDefinition.ReadAssembly(path, parameters);
			}
			catch {
				throw new DllNotFoundException($"Could not resolve {refName}");
			}
		}
	}
}