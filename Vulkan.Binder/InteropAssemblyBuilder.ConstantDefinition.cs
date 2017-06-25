using System;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<TypeDefinition[]> DefineClrType(ClangEnumInfo enumInfo) {
			var underlyingTypeInfo = ResolveParameter(enumInfo.UnderlyingType);
			var underlyingType = underlyingTypeInfo.Type;

			var name = enumInfo.Name;
			
			if (TypeRedirects.TryGetValue(name, out var renamed)) {
				name = renamed;
			}

			var enumTypeDef = Module.GetType(name);
			if (enumTypeDef == null) {
				enumTypeDef = Module.DefineEnum(name, TypeAttributes.Public, underlyingType);
				enumTypeDef.SetCustomAttribute(() => new CompilerGeneratedAttribute());
			}
			else
				enumTypeDef.ChangeUnderlyingType(underlyingType);
			//enumTypeDef.SetCustomAttribute(FlagsAttributeInfo);

			foreach (var enumDef in enumInfo.Definitions)
				enumTypeDef.DefineLiteral(enumDef.Name, Convert.ChangeType(enumDef.Value, underlyingType.GetRuntimeType()));

			var enumType = enumTypeDef.CreateType();

			return () => new[] {enumType};
		}

		private Func<TypeDefinition[]> DefineClrType(ClangEnumInfo enumInfo32, ClangEnumInfo enumInfo64) {
			throw new NotImplementedException();
		}

		private Func<TypeDefinition[]> DefineClrType(ClangFlagsInfo flagsInfo) {
			throw new NotImplementedException();
		}

		private Func<TypeDefinition[]> DefineClrType(ClangFlagsInfo flagsInfo32, ClangFlagsInfo flagsInfo64) {
			throw new NotImplementedException();
		}
	}
}