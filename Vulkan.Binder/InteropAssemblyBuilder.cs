using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using ClangSharp;
using Interop;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Vulkan.Binder.Extensions;
//using System.Reflection;
//using System.Reflection.Emit;
using SAssembly = System.Reflection.Assembly;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private static ConcurrentStack<Func<TypeDefinition[]>> DefinitionFuncs {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new ConcurrentStack<Func<TypeDefinition[]>>();

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

		public readonly TypeReference VoidPointerType;
		public readonly TypeReference ObjectType;
		public readonly TypeReference MulticastDelegateType;
		public readonly TypeReference BinderGeneratedAttributeType;

		public readonly TypeReference IntType;
		public readonly TypeReference UIntType;
		public readonly TypeReference LongType;
		public readonly TypeReference ULongType;
		public readonly TypeReference IntPtrType;
		public readonly TypeReference UIntPtrType;
		public readonly TypeReference ValueTypeType;

		public readonly TypeReference HandleInt32Gtd;
		public readonly TypeReference HandleUInt32Gtd;
		public readonly TypeReference HandleInt64Gtd;
		public readonly TypeReference HandleUInt64Gtd;
		public readonly TypeReference HandleIntPtrGtd;
		public readonly TypeReference HandleUIntPtrGtd;
		public readonly TypeReference IHandleGtd;
		public readonly TypeReference ITypedHandleGtd;
		public readonly TypeReference ITypedHandleType;
		public readonly TypeReference SplitPointerGtd;

		private static readonly ModuleParameters DefaultModuleParameters = new ModuleParameters {
			Architecture = TargetArchitecture.I386,
			Kind = ModuleKind.Dll,
			Runtime = TargetRuntime.Net_4_0
		};

		private static readonly string DefaultTargetFramework = ".NETStandard,Version=v1.6";
		private static readonly SAssembly BaseInteropAssembly = typeof(IHandle<>).GetTypeInfo().Assembly;
		private static readonly string BaseInteropAsmName = BaseInteropAssembly.GetName().Name;
		private static readonly string InteropAsmCodeBase = BaseInteropAssembly.CodeBase;
		private static readonly string BaseInteropAsmPath = new Uri(InteropAsmCodeBase).LocalPath;
		
		public ImmutableDictionary<string, KnownType> KnownTypes
			= ImmutableDictionary<string, KnownType>.Empty;

		public ImmutableDictionary<string, string> TypeRedirects
			= ImmutableDictionary<string, string>.Empty;

		private readonly string ModuleName;

		public InteropAssemblyBuilder(string assemblyName, string version = "1.0.0.0")
			: this(new AssemblyNameDefinition(assemblyName, Version.Parse(version))) {
		}

		public InteropAssemblyBuilder(AssemblyNameDefinition assemblyName, ModuleParameters moduleParams = null, string targetFramework = null) {
			Name = assemblyName;
			Namespace = assemblyName.Name;
			Statistics = new ReadOnlyDictionary<string, long>(_statistics);
			if (moduleParams == null)
				moduleParams = DefaultModuleParameters;
			
			// ReSharper disable once UnusedVariable
			var forceUnsafeToLoad = Unsafe.AreSame(ref assemblyName, ref assemblyName);
			
			//var loadedAsms = AppDomain.CurrentDomain.GetAssemblies();
			//var loadedAsms = AssemblyResolver.KnownAssemblies;

			var asmResolver = new AssemblyResolver();

			var mdResolver = new MetadataResolver(asmResolver);

			var shadowAsmFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"{BaseInteropAsmName}.dll");
			File.Copy(BaseInteropAsmPath, shadowAsmFilePath);
			
			Assembly = AssemblyDefinition.ReadAssembly(shadowAsmFilePath, new ReaderParameters {
				InMemory = true,
				ReadWrite = true,
				ReadingMode = ReadingMode.Immediate,
				AssemblyResolver = asmResolver,
				//MetadataResolver = mdResolver,
				ReadSymbols = false,
				ApplyWindowsRuntimeProjections = false,
				//SymbolReaderProvider = null,
				//MetadataImporterProvider = null,
				//ReflectionImporterProvider = null,
			});

			Assembly.Name = assemblyName;
			Module = Assembly.MainModule;
			ModuleName = $"{Name.Name}.dll";
			Module.Name = ModuleName;

			if (EmitBoundsChecks)
				ArgumentOutOfRangeCtor = ArgumentOutOfRangeCtorInfo.Import(Module);

			NonVersionableAttribute = NonVersionableAttributeInfo?
				.GetCecilCustomAttribute(Module);
			MethodImplAggressiveInliningAttribute = MethodImplAggressiveInliningAttributeInfo
				.GetCecilCustomAttribute(Module);
			FlagsAttribute = FlagsAttributeInfo
				.GetCecilCustomAttribute(Module);

			VoidPointerType = Module.TypeSystem.Void.MakePointerType();
			ObjectType = Module.TypeSystem.Object;
			IntType = Module.TypeSystem.Int32;
			UIntType = Module.TypeSystem.UInt32;
			LongType = Module.TypeSystem.Int64;
			ULongType = Module.TypeSystem.UInt64;
			IntPtrType = Module.TypeSystem.IntPtr;
			UIntPtrType = Module.TypeSystem.UIntPtr;
			ValueTypeType = UIntPtrType.Resolve().BaseType;

			MulticastDelegateType = typeof(MulticastDelegate).Import(Module);

			//var interopAsm = typeof(IHandle<>).Assembly;
			//var interopAsmName = interopAsm.GetName();
			//var interopMod = interopAsm.ManifestModule;

			//Module.AssemblyReferences.Add(new AssemblyNameReference(interopAsmName.Name, interopAsmName.Version));
			//Module.ModuleReferences.Add(new ModuleReference(interopAsmName.Name));

			
			ITypedHandleType = typeof(ITypedHandle).Import(Module);
			IHandleGtd = typeof(IHandle<>).Import(Module);
			ITypedHandleGtd = typeof(ITypedHandle<>).Import(Module);
			HandleInt32Gtd = typeof(HandleInt32<>).Import(Module);
			HandleUInt32Gtd = typeof(HandleUInt32<>).Import(Module);
			HandleInt64Gtd = typeof(HandleInt64<>).Import(Module);
			HandleUInt64Gtd = typeof(HandleUInt64<>).Import(Module);
			HandleIntPtrGtd =  typeof(HandleIntPtr<>).Import(Module);
			HandleUIntPtrGtd =  typeof(HandleUIntPtr<>).Import(Module);
			SplitPointerGtd = typeof(SplitPointer<,,>).Import(Module);
			BinderGeneratedAttributeType = typeof(BinderGeneratedAttribute).Import(Module);

			IntegrateInteropTypes(Module.Types);

			if (targetFramework == null)
				targetFramework = DefaultTargetFramework;
			
			var targetFrameworkAttr = typeof(TargetFrameworkAttribute).Import(Module);
			var asmInfoVersionAttr = typeof(AssemblyInformationalVersionAttribute).Import(Module);
			var asmFileVersionAttr = typeof(AssemblyFileVersionAttribute).Import(Module);
			var asmDescriptionAttr = typeof(AssemblyDescriptionAttribute).Import(Module);
			var asmProductAttr = typeof(AssemblyProductAttribute).Import(Module);
			var asmTitleAttr = typeof(AssemblyTitleAttribute).Import(Module);
			var stringType = Module.TypeSystem.String;
			var attrActionMapBuilder = ImmutableDictionary
				.CreateBuilder<TypeReference, Action<CustomAttribute>>(CecilTypeComparer.Instance);
			attrActionMapBuilder.Add(targetFrameworkAttr,
				ca => ca.ConstructorArguments[0]
					= new CustomAttributeArgument( stringType, targetFramework ));
			attrActionMapBuilder.Add(asmInfoVersionAttr,
				ca => ca.ConstructorArguments[0]
					= new CustomAttributeArgument( stringType, Name.Version.ToString() ));
			attrActionMapBuilder.Add(asmFileVersionAttr,
				ca => ca.ConstructorArguments[0]
					= new CustomAttributeArgument( stringType, Name.Version.ToString() ));
			attrActionMapBuilder.Add(asmProductAttr,
				ca => ca.ConstructorArguments[0]
					= new CustomAttributeArgument( stringType, Name.Name ));
			attrActionMapBuilder.Add(asmTitleAttr,
				ca => ca.ConstructorArguments[0]
					= new CustomAttributeArgument( stringType, Name.Name ));
			attrActionMapBuilder.Add(asmDescriptionAttr,
				ca => ca.ConstructorArguments[0]
					= new CustomAttributeArgument( stringType, Name.Name ));

			var attrActionMap = attrActionMapBuilder.ToImmutable();
			var custAttrs = Assembly.CustomAttributes.Select(old => {
				var @new = new CustomAttribute(old.Constructor);
				foreach (var arg in old.ConstructorArguments)
					@new.ConstructorArguments.Add(arg);
				foreach (var field in old.Fields)
					@new.Fields.Add(field);
				foreach (var prop in old.Properties)
					@new.Fields.Add(prop);
				if (attrActionMap.TryGetValue(@new.AttributeType.Resolve(), out var act))
					act(@new);
				return @new;
			}).ToArray();
			while ( Assembly.HasCustomAttributes )
				Assembly.CustomAttributes.RemoveAt(0);
			Assembly.CustomAttributes.Clear();
			foreach (var custAttr in custAttrs)
				Assembly.CustomAttributes.Add(custAttr);
			
		}

		public void Compile() {
			PrepareKnownTypes();
			ParseUnits();
			BuildTypeDefinitions();
			
		}

		private void PrepareKnownTypes() {
			var index = 0;
			var total = KnownTypes.Count;
			foreach (var knownType in KnownTypes) {
				ReportProgress("Preparing known types", index++, total);
				var name = knownType.Key;
				if (TypeRedirects.TryGetValue(name, out var renamed)) {
					name = renamed;
				}
				if ( Module.GetType(name) != null )
					throw new NotImplementedException();
				switch (knownType.Value) {
					case KnownType.Enum: {
						Module.DefineEnum(name, TypeAttributes.Public);
						break;
					}
					case KnownType.Bitmask: {
						var def = Module.DefineEnum(name, TypeAttributes.Public);
						def.CustomAttributes.Add(FlagsAttribute);
						break;
					}
					case KnownType.Handle: {
						throw new NotImplementedException();
					}
					case KnownType.Struct: {
						throw new NotImplementedException();
					}
				}
			}
			ReportProgress("Preparing known types", index, total);
		}

		public FileInfo Save(string outputPath) {
			// ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
			var writerParams = new WriterParameters {
				// TODO: strong name key
			};
			// coalesce assembly references
			var typeRefs = Module.GetTypeReferences().ToArray();

			var anrCollisions = Module.AssemblyReferences.GroupBy(asmRef => asmRef.Name);
			foreach (var anrCollision in anrCollisions) {
				var name = anrCollision.Key;
				var ordered = anrCollision.OrderByDescending(anr => anr.Version).ToArray();
				var latest = ordered.First();
				var others = ordered.Skip(1).ToArray();
				if (others.Length <= 0) continue;
				foreach (var tr in typeRefs) {
					if (tr.Scope is AssemblyNameReference anr) {
						//tr.MetadataToken = new MetadataToken(TokenType.AssemblyRef, 0);
						tr.MetadataToken = new MetadataToken(TokenType.TypeRef, 0);
						tr.Scope = latest;
						ReportProgress($"Retargeting {tr.FullName} from {latest.Name} v{anr.Version} to v{latest.Version}");
						continue;
					}
					/*
					if (tr.Scope is ModuleDefinition scope) {
						var asm = scope.Assembly;
						var asmName = asm.Name;
						if (asmName.Name != name)
							continue;
						var other = others.FirstOrDefault(anr => anr.Version == asmName.Version);
						if (other == null)
							continue;
						// update type ref to point to latest asm
						throw new NotImplementedException();
					}
					*/
					throw new NotImplementedException();
				}
				foreach (var other in others)
					Module.AssemblyReferences.Remove(other);
			}

			var fileName = ModuleName;
			if (File.Exists(fileName))
				File.Delete(fileName);
			Assembly.Write(fileName, writerParams);
			var outputFilePath = Path.Combine(outputPath, fileName);
			if (File.Exists(outputFilePath))
				File.Delete(outputFilePath);
			File.Move(fileName, outputFilePath);
			return new FileInfo(outputFilePath);
		}

		public FileInfo CompileAndSave(string outputPath) {
			Compile();
			return Save(outputPath);
		}


		public enum KnownType {
			Enum,
			Bitmask,
			Struct,
			Handle
		}

		public Action<string, int, int> ProgressReportFunc { get; set; } = null;

		private void ReportProgress(string state, int workDone = -1, int workTotal = -1) {
			ProgressReportFunc?.Invoke(state, workDone, workTotal);
		}
	}
}