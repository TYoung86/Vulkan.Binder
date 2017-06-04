using System;
using System.Reflection;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<Type[]> DefineClrType(ClangEnumInfo enumInfo) {
			var underlyingTypeInfo = ResolveParameter(enumInfo.UnderlyingType);
			var underlyingType = underlyingTypeInfo.ParameterType;

			var enumTypeDef = Module.DefineEnum(enumInfo.Name, TypeAttributes.Public, underlyingType);
			enumTypeDef.SetCustomAttribute(FlagsAttributeInfo);

			foreach (var enumDef in enumInfo.Definitions)
				enumTypeDef.DefineLiteral(enumDef.Name, Convert.ChangeType(enumDef.Value, underlyingType));

			var enumType = enumTypeDef.CreateType();

			return () => new[] {enumType};
		}

		private Func<Type[]> DefineClrType(ClangEnumInfo enumInfo32, ClangEnumInfo enumInfo64) {
			throw new NotImplementedException();
		}

		private Func<Type[]> DefineClrType(ClangFlagsInfo flagsInfo) {
			throw new NotImplementedException();
		}

		private Func<Type[]> DefineClrType(ClangFlagsInfo flagsInfo32, ClangFlagsInfo flagsInfo64) {
			throw new NotImplementedException();
		}
	}
}