using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private static readonly Type IntType = typeof(int);
		private static readonly Type ULongType = typeof(ulong);
		private static readonly Type LongType = typeof(long);
		private static readonly Type UIntType = typeof(uint);
		private static readonly Type HandleIntType = typeof(HandleInt<>);
		private static readonly Type HandleUIntType = typeof(HandleUInt<>);
		private static readonly Type HandleLongType = typeof(HandleLong<>);
		private static readonly Type HandleULongType = typeof(HandleULong<>);

		private static PropertyBuilder DefineInterfaceGetHandleProperty(TypeBuilder interfaceDef, Type handleType, string propName, IEnumerable<TypeTransform> transforms = null) {

			if (transforms != null)
				foreach (var transform in transforms)
					transform(ref handleType);

			if (handleType.IsArray) {
				// TODO: ref index implementation
				throw new NotImplementedException();
			}
			//var propType = propElemType.MakePointerType();
			var propRefType = handleType.MakeByRefType();
			return DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
		}

		private static MethodBuilder DefineInterfaceGetByIndexMethod(TypeBuilder interfaceDef, Type propElemType, string propName) {

			var getter = interfaceDef.DefineMethod(propName,
				InterfaceMethodAttributes,
				propElemType, new[] {IntType});
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

		private static void BuildInterfaceByRefAccessor(
			TypeBuilder interfaceDef, string propName,
			ConcurrentDictionary<string,Type> splitPointerDefs,
			LinkedListNode<CustomParameterInfo> fieldInfo32,
			LinkedListNode<CustomParameterInfo> fieldInfo64,
			out PropertyBuilder interfacePropDef,
			out MethodBuilder interfaceMethodDef
			) {
			var fieldType32 = fieldInfo32.Value.ParameterType;
			var fieldType64 = fieldInfo64.Value.ParameterType;

			interfacePropDef = null;
			interfaceMethodDef = null;
			
			LinkedList<TypeTransform> transforms32;
			LinkedList<TypeTransform> transforms64;

			if (fieldType32 == IntType && fieldType64 == LongType) {
				// IntPtr
				var propType = typeof(IntPtr);
				var propRefType = propType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}
			if (fieldType32 == UIntType && fieldType64 == ULongType) {
				// UIntPtr
				var propType = typeof(UIntPtr);
				var propRefType = propType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}

			if (fieldType32 == fieldType64 && fieldType32.IsDirect()) {
				// same type
				var propType = fieldType32;
				var propRefType = propType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}
			

			var fieldElemType32 = GetTypePointedTo(fieldType32, out transforms32);
			var fieldElemType64 = GetTypePointedTo(fieldType64, out transforms64);

			var pointerDepth32 = transforms32.Count;
			var pointerDepth64 = transforms64.Count;

			if ( Math.Abs(pointerDepth32 - pointerDepth64) > 1 )
				throw new NotSupportedException();

			if (fieldType32.IsPointer && fieldType64 == IntType) {
				// 32-bit handle
				var handleType = HandleIntType.MakeGenericType(fieldElemType32);
				
				fieldInfo32.Value.ParameterType = handleType;
				fieldInfo64.Value.ParameterType = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}
			if (fieldType32.IsPointer && fieldType64 == UIntType) {
				// 32-bit handle
				var handleType = HandleUIntType.MakeGenericType(fieldElemType32);
				
				fieldInfo32.Value.ParameterType = handleType;
				fieldInfo64.Value.ParameterType = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}
			if (fieldType32 == LongType && fieldType64.IsPointer) {
				// 64-bit handle
				var handleType = HandleLongType.MakeGenericType(fieldElemType64);
				
				fieldInfo32.Value.ParameterType = handleType;
				fieldInfo64.Value.ParameterType = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}
			if (fieldType32 == ULongType && fieldType64.IsPointer) {
				// 64-bit handle
				var handleType = HandleULongType.MakeGenericType(fieldElemType64);
				
				fieldInfo32.Value.ParameterType = handleType;
				fieldInfo64.Value.ParameterType = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}

			if (fieldElemType64 == IntType && IsHandleType(fieldElemType32)) {
				// 32-bit handle
				var handleType = HandleIntType.MakeGenericType(fieldElemType32);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.ParameterType = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo64.Value.ParameterType = updatedFieldType64;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms32.Skip(1));
				return;
			}
			if (fieldElemType64 == UIntType && IsHandleType(fieldElemType32)) {
				// 32-bit handle
				var handleType = HandleUIntType.MakeGenericType(fieldElemType32);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.ParameterType = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo64.Value.ParameterType = updatedFieldType64;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms32.Skip(1));
				return;
			}
			if (fieldElemType32 == LongType && IsHandleType(fieldElemType64)) {
				// 64-bit handle
				var handleType = HandleLongType.MakeGenericType(fieldElemType64);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.ParameterType = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo32.Value.ParameterType = updatedFieldType32;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms64.Skip(1));
				return;
			}
			if (fieldElemType32 == ULongType && IsHandleType(fieldElemType64)) {
				// 64-bit handle
				var handleType = HandleULongType.MakeGenericType(fieldElemType64);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.ParameterType = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo32.Value.ParameterType = updatedFieldType32;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms64.Skip(1));
				return;
			}

			if (fieldType32.IsPointer && fieldType64.IsPointer && fieldElemType32 == fieldElemType64) {
				if (fieldElemType32.SizeOf() == 0)
					throw new NotImplementedException();

				var propRefType = fieldElemType32;
				foreach (var transform in transforms32)
					transform(ref propRefType);
				propRefType = propRefType.MakeByRefType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}

			var interiorType32 = FindInteriorType(fieldElemType32, ref transforms32);
			var interiorType64 = FindInteriorType(fieldElemType64, ref transforms64);

			if (!transforms32.SequenceEqual(transforms64)) {
				throw new NotImplementedException();
			}

			if (fieldType32 == fieldType64 && fieldType32.IsArray) {
				// ref index implementation

				if (interiorType32.SizeOf() == 0)
					throw new NotImplementedException();

				var fieldElemType = fieldType64.GetElementType();

				if (IsHandleType(interiorType64)) {
					var handleType = typeof(HandleUIntPtr<>).MakeGenericType(interiorType64);
					var handleElemType = handleType;
					foreach (var transform in transforms64.Take(transforms64.Count - 2))
						transform(ref handleElemType);
					var handleElemRefType = handleElemType.MakeByRefType();
					interfaceMethodDef =
						DefineInterfaceGetByIndexMethod(interfaceDef, handleElemRefType, propName);
					return;
				}


				var propElemRefType = fieldElemType.MakeByRefType();

				interfaceMethodDef =
					DefineInterfaceGetByIndexMethod(interfaceDef, propElemRefType, propName);
				return;
			}
			if (fieldType32.IsArray || fieldType64.IsArray) {
				// TODO: ...
				throw new NotImplementedException();
			}
			var fieldInterfaces32 = interiorType32.GetInterfaces();
			var fieldInterfaces64 = interiorType64.GetInterfaces();
			var commonInterfaces = fieldInterfaces32.Intersect(fieldInterfaces64).ToArray();
			if (commonInterfaces.Length == 0) {
				// object, boxing reference
				throw new NotImplementedException();
				//DefineInterfaceGetSetProperty(interfaceDef, propType, propName);
			}
			if (commonInterfaces.Length > 1) {
				// TODO: multiple common interface, boxing reference
				throw new NotImplementedException();
			}

			/* commonInterfaces.Length == 1 */

			if (transforms32.First() == MakePointerType) {
				// common interface, boxing reference
				var commonInterface = commonInterfaces.First();
				splitPointerDefs.TryGetValue(commonInterface.FullName, out var splitPointerDef);
				var propType = splitPointerDef ?? typeof(SplitPointer<,,>).MakeGenericType(commonInterface, interiorType32, interiorType64);
				foreach (var transform in transforms32.Skip(1))
					transform(ref propType);
				var propRefType = propType.MakeByRefType();
				interfacePropDef = DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}
			throw new NotImplementedException();
		}
		
	}
}