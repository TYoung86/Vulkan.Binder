using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using ClangSharp;

namespace Artilect.Vulkan.Binder {

	public partial class InteropAssemblyBuilder {

		private static ConcurrentBag<Func<Type[]>> DefinitionFuncs {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new ConcurrentBag<Func<Type[]>>();

		private ConcurrentDictionary<string, CXTranslationUnit> Units32 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new ConcurrentDictionary<string, CXTranslationUnit>();

		private ConcurrentDictionary<string, CXTranslationUnit> Units64 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new ConcurrentDictionary<string, CXTranslationUnit>();

		private CXIndex ClangIndex32 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = clang.createIndex(0, 0);

		private CXIndex ClangIndex64 {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = clang.createIndex(0, 0);

		public AssemblyName Name { get; }

		private AssemblyBuilder Assembly { get; }

		private ModuleBuilder Module { get; }

		public bool EmitBoundsChecks { get; set; } = true;

		//public TypeBuilder Delegates { get; }

		public InteropAssemblyBuilder(string assemblyName)
			: this(new AssemblyName(assemblyName)) {
		}

		public InteropAssemblyBuilder(AssemblyName assemblyName) {
			Name = assemblyName;
			Statistics = new ReadOnlyDictionary<string, long>(_statistics);
			Assembly = AssemblyBuilder.DefineDynamicAssembly(Name, AssemblyBuilderAccess.Save);
			Assembly.SetCustomAttribute(AttributeInfo.Create(() => new TargetFrameworkAttribute("")));
			var moduleName = assemblyName.Name;
			Module = Assembly.DefineDynamicModule(moduleName, $"{moduleName}.dll");
			//Delegates = Module.DefineType("Delegates", TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Public);
		}

		public void Compile() {
			ParseUnits();
			BuildTypeDefinitions();
		}

		public FileInfo Save(string outputPath) {
			Assembly.Save($"{Name.Name}.dll");
			var asm = new FileInfo($"{Name.Name}.dll");
			asm.MoveTo(Path.Combine(outputPath, asm.Name));
			return asm;
		}

		public FileInfo CompileAndSave(string outputPath) {
			Compile();
			return Save(outputPath);
		}
	}
}