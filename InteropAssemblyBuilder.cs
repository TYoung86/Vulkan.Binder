using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
//using System.Reflection;
//using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;
using ClangSharp;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using SAssembly = System.Reflection.Assembly;
using TypeAttributes = Mono.Cecil.TypeAttributes;

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

		public readonly TypeDefinition HandleInt32Gtd;
		public readonly TypeDefinition HandleUInt32Gtd;
		public readonly TypeDefinition HandleInt64Gtd;
		public readonly TypeDefinition HandleUInt64Gtd;
		public readonly TypeDefinition HandleIntPtrGtd;
		public readonly TypeDefinition HandleUIntPtrGtd;
		public readonly TypeDefinition IHandleGtd;
		public readonly TypeDefinition ITypedHandleGtd;
		public readonly TypeDefinition ITypedHandleType;
		public readonly TypeDefinition SplitPointerGtd;

		private static readonly ModuleParameters DefaultModuleParameters = new ModuleParameters {
			Architecture = TargetArchitecture.I386,
			Kind = ModuleKind.Dll,
			Runtime = TargetRuntime.Net_4_0
		};

		private static readonly string DefaultTargetFramework = ".NETStandard,Version=v1.6";
		private static readonly SAssembly BaseInteropAssembly = typeof(IHandle<>).Assembly;
		private static readonly string BaseInteropAsmName = BaseInteropAssembly.GetName().Name;
		private static readonly string InteropAsmCodeBase = BaseInteropAssembly.CodeBase;
		private static readonly string BaseInteropAsmPath = new Uri(InteropAsmCodeBase).LocalPath;

		public ImmutableDictionary<string, KnownType> KnownTypes
			= ImmutableDictionary<string, KnownType>.Empty;

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
			
			var asmResolver = new DefaultAssemblyResolver();
			asmResolver.ResolveFailure += (sender, reference) => {
				if (reference.Name == BaseInteropAsmName)
					return Assembly;
				throw new NotImplementedException();
			};
			var mdResolver = new MetadataResolver(asmResolver);

			var shadowAsmFilePath = Path.Combine(Environment.CurrentDirectory, $"{BaseInteropAsmName}.dll");
			File.Copy(BaseInteropAsmPath, shadowAsmFilePath);
			Assembly = AssemblyDefinition.ReadAssembly(shadowAsmFilePath, new ReaderParameters {
				InMemory = true,
				ReadWrite = true,
				ReadingMode = ReadingMode.Immediate,
				AssemblyResolver = asmResolver,
				MetadataResolver = mdResolver,
			});

			Assembly.Name = assemblyName;
			Module = Assembly.MainModule;
			ModuleName = $"{Name.Name}.dll";
			Module.Name = ModuleName;
			//asmResolver.AddSearchDirectory(Path.GetDirectoryName(asmPath));
			//Assembly = AssemblyDefinition.CreateAssembly(Name, Namespace, moduleParams);

			if (EmitBoundsChecks)
				ArgumentOutOfRangeCtor = Module.ImportReference(ArgumentOutOfRangeCtorInfo);

			NonVersionableAttribute = NonVersionableAttributeInfo?
				.GetCecilCustomAttribute(Module);
			MethodImplAggressiveInliningAttribute = MethodImplAggressiveInliningAttributeInfo
				.GetCecilCustomAttribute(Module);
			FlagsAttribute = FlagsAttributeInfo
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

			
			ITypedHandleType = typeof(ITypedHandle).Import(Module).Resolve();
			IHandleGtd = typeof(IHandle<>).Import(Module).Resolve();
			ITypedHandleGtd = typeof(ITypedHandle<>).Import(Module).Resolve();
			HandleInt32Gtd = typeof(HandleInt32<>).Import(Module).Resolve();
			HandleUInt32Gtd = typeof(HandleUInt32<>).Import(Module).Resolve();
			HandleInt64Gtd = typeof(HandleInt64<>).Import(Module).Resolve();
			HandleUInt64Gtd = typeof(HandleUInt64<>).Import(Module).Resolve();
			HandleIntPtrGtd =  typeof(HandleIntPtr<>).Import(Module).Resolve();
			HandleUIntPtrGtd =  typeof(HandleUIntPtr<>).Import(Module).Resolve();
			SplitPointerGtd = typeof(SplitPointer<,,>).Import(Module).Resolve();

			IntegrateInteropTypes(Module.Types);

			if (targetFramework == null)
				targetFramework = DefaultTargetFramework;
			
			var targetFrameworkAttr = typeof(TargetFrameworkAttribute).Import(Module).Resolve();
			var asmInfoVersionAttr = typeof(AssemblyInformationalVersionAttribute).Import(Module).Resolve();
			var asmFileVersionAttr = typeof(AssemblyFileVersionAttribute).Import(Module).Resolve();
			var asmDescriptionAttr = typeof(AssemblyDescriptionAttribute).Import(Module).Resolve();
			var asmProductAttr = typeof(AssemblyProductAttribute).Import(Module).Resolve();
			var asmTitleAttr = typeof(AssemblyTitleAttribute).Import(Module).Resolve();
			var stringType = typeof(string).Import(Module).Resolve();
			var attrActionMapBuilder = ImmutableDictionary.CreateBuilder<TypeDefinition, Action<CustomAttribute>>();
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
			foreach (var knownType in KnownTypes) {
				switch (knownType.Value) {
					case KnownType.Enum: {
						Module.DefineEnum(knownType.Key, TypeAttributes.Public);
						break;
					}
					case KnownType.Bitmask: {
						var def = Module.DefineEnum(knownType.Key, TypeAttributes.Public);
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
		}

		public FileInfo Save(string outputPath) {
			// ReSharper disable once RedundantEmptyObjectOrCollectionInitializer
			var writerParams = new WriterParameters {
				// TODO: strong name key
			};
			var asmRef = Module.AssemblyReferences
				.FirstOrDefault(mr => mr.Name.StartsWith(BaseInteropAsmName));
			if (asmRef != null)
				Module.AssemblyReferences.Remove(asmRef);
			//Module.Write(ModuleName, writerParams);
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
	}
}