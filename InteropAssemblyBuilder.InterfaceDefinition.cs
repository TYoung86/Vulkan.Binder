using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private static PropertyBuilder DefineInterfaceGetHandleProperty(TypeBuilder interfaceDef, Type fieldElemType, Type handleType, string propName, IEnumerable<TypeTransform> transforms = null) {
			if ( handleType == null || fieldElemType.IsPointer )
				throw new Exception("Doh");
			var propElemType = handleType
				.MakeGenericType(fieldElemType);

			if (transforms != null)
				foreach (var transform in transforms)
					transform(ref propElemType);

			if (propElemType.IsArray) {
				// TODO: ref index implementation
				throw new NotImplementedException();
			}
			//var propType = propElemType.MakePointerType();
			var propRefType = propElemType.MakeByRefType();
			return DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
		}

		private static MethodBuilder DefineInterfaceGetByIndexMethod(TypeBuilder interfaceDef, Type propElemType, string propName) {

			var getter = interfaceDef.DefineMethod(propName,
				InterfaceMethodAttributes,
				propElemType, new[] {typeof(int)});
			SetMethodInliningAttributes(getter);

			return getter;
		}

		private static PropertyBuilder DefineInterfaceGetSetProperty(TypeBuilder interfaceDef, Type propType, string propName) {
			var propDef = interfaceDef.DefineProperty(propName, PropertyAttributes.None,
				propType, null);
			var propGetter = interfaceDef.DefineMethod("get_" + propName,
				InterfaceMethodAttributes,
				propType, Type.EmptyTypes);
			SetMethodInliningAttributes(propGetter);
			var propSetter = interfaceDef.DefineMethod("set_" + propName,
				InterfaceMethodAttributes,
				typeof(void), new[] {propType});
			SetMethodInliningAttributes(propSetter);
			propDef.SetGetMethod(propGetter);
			propDef.SetSetMethod(propSetter);
			return propDef;
		}

		private static PropertyBuilder DefineInterfaceGetProperty(TypeBuilder interfaceDef, Type propType, string propName) {
			var propDef = interfaceDef.DefineProperty(propName,
				PropertyAttributes.None,
				propType, null);
			var propGetter = interfaceDef.DefineMethod("get_" + propName,
				InterfaceMethodAttributes,
				propType, Type.EmptyTypes);
			SetMethodInliningAttributes(propGetter);
			propDef.SetGetMethod(propGetter);
			return propDef;
		}
	}
}