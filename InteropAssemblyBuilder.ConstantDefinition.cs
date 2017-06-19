using System;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<TypeDefinition[]> DefineClrType(ClangEnumInfo enumInfo) {
			var underlyingTypeInfo = ResolveParameter(enumInfo.UnderlyingType);
			var underlyingType = underlyingTypeInfo.Type;

			var enumTypeDef = Module.GetType(enumInfo.Name);
			if (enumTypeDef == null)
				Module.DefineEnum(enumInfo.Name, TypeAttributes.Public, underlyingType);
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