using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
//using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;
using ClangSharp;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private static ConcurrentBag<Func<TypeDefinition[]>> DefinitionFuncs {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new ConcurrentBag<Func<TypeDefinition[]>>();

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

		public AssemblyNameDefinition Name { get; }

		//private AssemblyBuilder Assembly { get; }
		public AssemblyDefinition Assembly { get; }

		//private ModuleBuilder Module { get; }
		public ModuleDefinition Module { get; }

		public string Namespace { get; }

		public bool EmitBoundsChecks { get; set; } = true;

		//public TypeBuilder Delegates { get; }

		public readonly TypeDefinition VoidPointerType;
		public readonly TypeDefinition ObjectType;

		public readonly TypeDefinition IntType;
		public readonly TypeDefinition UIntType;
		public readonly TypeDefinition LongType;
		public readonly TypeDefinition ULongType;
		public readonly TypeDefinition IntPtrType;
		public readonly TypeDefinition UIntPtrType;
		public readonly TypeDefinition ValueTypeType;

		public readonly TypeDefinition HandleInt32Type;
		public readonly TypeDefinition HandleUInt32Type;
		public readonly TypeDefinition HandleInt64Type;
		public readonly TypeDefinition HandleUInt64Type;
		public readonly TypeDefinition IHandleGtd;
		public readonly TypeDefinition ITypedHandleGtd;
		public readonly TypeDefinition ITypedHandle;

		private static readonly ModuleParameters DefaultModuleParameters = new ModuleParameters {
			Architecture = TargetArchitecture.I386,
			Kind = ModuleKind.Dll,
			Runtime = TargetRuntime.Net_4_0
		};

		private static readonly string DefaultTargetFramework = ".NETStandard,Version=v1.6";
		private static readonly Assembly BaseInteropAssembly = typeof(IHandle<>).Assembly;
		private static readonly string BaseInteropAsmName = BaseInteropAssembly.GetName().Name;
		private static readonly string InteropAsmCodeBase = BaseInteropAssembly.CodeBase;
		private static readonly string BaseInteropAsmPath = new Uri(InteropAsmCodeBase).LocalPath;

		public InteropAssemblyBuilder(string assemblyName, string version = "1.0.0.0")
			: this(new AssemblyNameDefinition(assemblyName, Version.Parse(version))) {
		}

		public InteropAssemblyBuilder(AssemblyNameDefinition assemblyName, ModuleParameters moduleParams = null, string targetFramework = null) {
			Name = assemblyName;
			Namespace = assemblyName.Name;
			Statistics = new ReadOnlyDictionary<string, long>(_statistics);
			if ( moduleParams == null )
				moduleParams = DefaultModuleParameters;


			Assembly = AssemblyDefinition.ReadAssembly(BaseInteropAsmPath);
			Assembly.Name = assemblyName;
			Module = Assembly.MainModule;
			var asmResolver = (BaseAssemblyResolver) Module.AssemblyResolver;
			asmResolver.ResolveFailure += (sender, reference) => {
				if ( reference.Name == BaseInteropAsmName )
					return Assembly;
				throw new NotImplementedException();
			};
			//asmResolver.AddSearchDirectory(Path.GetDirectoryName(asmPath));
			//Assembly = AssemblyDefinition.CreateAssembly(Name, Namespace, moduleParams);

			if ( EmitBoundsChecks )
				ArgumentOutOfRangeCtor = Module.ImportReference(ArgumentOutOfRangeCtorInfo);
			
			NonVersionableAttribute = NonVersionableAttributeInfo?
				.GetCecilCustomAttribute(Module);
			MethodImplAggressiveInliningAttribute = MethodImplAggressiveInliningAttributeInfo
				.GetCecilCustomAttribute(Module);
			
			VoidPointerType = typeof(void*).Import(Module).Resolve();
			ObjectType = typeof(object).Import(Module).Resolve();
			IntType = typeof(int).Import(Module).Resolve();
			UIntType = typeof(uint).Import(Module).Resolve();
			LongType = typeof(long).Import(Module).Resolve();
			ULongType = typeof(ulong).Import(Module).Resolve();
			IntPtrType = typeof(IntPtr).Import(Module).Resolve();
			UIntPtrType = typeof(UIntPtr).Import(Module).Resolve();
			ValueTypeType = typeof(ValueType).Import(Module).Resolve();

			//var interopAsm = typeof(IHandle<>).Assembly;
			//var interopAsmName = interopAsm.GetName();
			//var interopMod = interopAsm.ManifestModule;

			//Module.AssemblyReferences.Add(new AssemblyNameReference(interopAsmName.Name, interopAsmName.Version));
			//Module.ModuleReferences.Add(new ModuleReference(interopAsmName.Name));



			IHandleGtd = typeof(IHandle<>).Import(Module).Resolve();
			ITypedHandle = typeof(ITypedHandle).Import(Module).Resolve();
			ITypedHandleGtd = typeof(ITypedHandle<>).Import(Module).Resolve();
			HandleInt32Type = typeof(HandleInt32<>).Import(Module).Resolve();
			HandleUInt32Type = typeof(HandleUInt32<>).Import(Module).Resolve();
			HandleInt64Type = typeof(HandleInt64<>).Import(Module).Resolve();
			HandleUInt64Type = typeof(HandleUInt64<>).Import(Module).Resolve();
			
			IntegrateInteropTypes(Module.Types);

			if ( targetFramework == null )
				targetFramework = DefaultTargetFramework;
			var targetFrameworkAttributeImport = typeof(TargetFrameworkAttribute).Import(Module);
			var targetFrameworkAttributeType = targetFrameworkAttributeImport.Resolve();
			var targetFrameworkAttribute = Assembly.CustomAttributes
				.FirstOrDefault(ca => ca.Constructor.DeclaringType.Resolve() == targetFrameworkAttributeType);
			if (targetFrameworkAttribute == null) {
				Assembly.CustomAttributes
					.Add(AttributeInfo.Create
						(() => new TargetFrameworkAttribute(targetFramework))
						.GetCecilCustomAttribute(Module));
			}
			else {
				var arg0 = targetFrameworkAttribute.ConstructorArguments[0];
				targetFrameworkAttribute.ConstructorArguments[0]
					= new CustomAttributeArgument(arg0.Type, targetFramework);
			}


		}

		public void Compile() {
			ParseUnits();
			BuildTypeDefinitions();
		}

		public FileInfo Save(string outputPath) {
			// ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
			var writerParams = new WriterParameters {
				// TODO: strong name key
			};
			var asmRef = Module.AssemblyReferences
				.FirstOrDefault(mr => mr.Name.StartsWith(BaseInteropAsmName));
			if ( asmRef != null )
				Module.AssemblyReferences.Remove(asmRef);
			//Module.Write($"{Name.Name}.dll", writerParams);
			var fileName = $"{Name.Name}.dll";
			if ( File.Exists(fileName) )
				File.Delete(fileName);
			Assembly.Write(fileName, writerParams);
			var outputFilePath = Path.Combine(outputPath, fileName);
			if ( File.Exists(outputFilePath) )
				File.Delete(outputFilePath);
			File.Move(fileName, outputFilePath);
			return new FileInfo(outputFilePath);
		}

		public FileInfo CompileAndSave(string outputPath) {
			Compile();
			return Save(outputPath);
		}
	}
}