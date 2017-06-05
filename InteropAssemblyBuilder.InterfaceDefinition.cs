using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;

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
			getter.DefineParameter(1, ParameterAttributes.In, "index");
			SetMethodInliningAttributes(getter);

			return getter;
		}

		/* not used yet
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
		*/

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

		private static bool TryDefineSimpleInterfaceRefProperty(TypeBuilder interfaceDef, string propName, Type fieldType32, Type fieldType64, out Type fieldElemType32, out Type fieldElemType64, out LinkedList<TypeTransform> transform32, out LinkedList<TypeTransform> transform64, out PropertyBuilder interfacePropDef) {
			interfacePropDef = null;
			fieldElemType32 = null;
			fieldElemType64 = null;
			transform32 = null;
			transform64 = null;

			if (fieldType32 == typeof(int) && fieldType64 == typeof(long)) {
				// IntPtr
				var propType = typeof(IntPtr);
				var propRefType = propType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return true;
			}
			if (fieldType32 == typeof(uint) && fieldType64 == typeof(ulong)) {
				// UIntPtr
				var propType = typeof(UIntPtr);
				var propRefType = propType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return true;
			}

			if (fieldType32 == fieldType64 && fieldType32.IsDirect()) {
				// same type
				var propType = fieldType32;
				var propRefType = propType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return true;
			}

			if (TryDefineInterfaceGetHandleProperty(interfaceDef, propName,
				fieldType32, fieldType64,
				ref interfacePropDef))
				return true;

			fieldElemType32 = GetTypePointedTo(fieldType32, out transform32);
			fieldElemType64 = GetTypePointedTo(fieldType64, out transform64);

			//var pointerDepth32 = transform32.Count;
			//var pointerDepth64 = transform64.Count;

			if (fieldElemType64 == typeof(int) && IsHandleType(fieldElemType32)) {
				// 32-bit handle
				var handleType = typeof(HandleInt<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType32, handleType, propName, transform32.Skip(1));
				return true;
			}
			if (fieldElemType64 == typeof(uint) && IsHandleType(fieldElemType32)) {
				// 32-bit handle
				var handleType = typeof(HandleUInt<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType32, handleType, propName, transform32.Skip(1));
				return true;
			}
			if (fieldElemType32 == typeof(long) && IsHandleType(fieldElemType64)) {
				// 64-bit handle
				var handleType = typeof(HandleLong<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType64, handleType, propName, transform64.Skip(1));
				return true;
			}
			if (fieldElemType32 == typeof(ulong) && IsHandleType(fieldElemType64)) {
				// 64-bit handle
				var handleType = typeof(HandleULong<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType64, handleType, propName, transform64.Skip(1));
				return true;
			}
			return false;
		}

		private static bool TryDefineInterfaceGetHandleProperty(TypeBuilder interfaceDef, string propName, Type fieldType32, Type fieldType64, ref PropertyBuilder interfacePropDef) {
			if (fieldType32.IsPointer && fieldType64 == typeof(int)) {
				// 32-bit handle
				var handleType = typeof(HandleInt<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldType32.GetElementType(), handleType, propName);
				return true;
			}
			if (fieldType32.IsPointer && fieldType64 == typeof(uint)) {
				// 32-bit handle
				var handleType = typeof(HandleUInt<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldType32.GetElementType(), handleType, propName);
				return true;
			}
			if (fieldType32 == typeof(long) && fieldType64.IsPointer) {
				// 64-bit handle
				var handleType = typeof(HandleLong<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldType64.GetElementType(), handleType, propName);
				return true;
			}
			if (fieldType32 == typeof(ulong) && fieldType64.IsPointer) {
				// 64-bit handle
				var handleType = typeof(HandleULong<>);

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, fieldType64.GetElementType(), handleType, propName);
				return true;
			}
			return false;
		}
	}
}