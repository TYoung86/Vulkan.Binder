using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using static Artilect.Vulkan.Binder.Extensions.CecilExtensions;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {

		private PropertyDefinition DefineInterfaceGetHandleProperty(
			TypeDefinition interfaceDef,
			TypeReference handleType,
			string propName,
			IEnumerable<TypeReferenceTransform> transforms = null) {

			if (transforms != null)
				foreach (var transform in transforms)
					transform(ref handleType);

			if (handleType.IsArray) {
				// TODO: ref index implementation
				throw new NotImplementedException();
			}
			//var propType = propElemType.MakePointerType();
			var propRefType = handleType.MakeByReferenceType();
			return DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
		}

		private MethodDefinition DefineInterfaceGetByIndexMethod(TypeDefinition interfaceDef, TypeReference propElemType, string propName) {

			var getter = interfaceDef.DefineMethod(propName,
				InterfaceMethodAttributes,
				propElemType, IntType);
			getter.DefineParameter(1, ParameterAttributes.In, "index");
			SetMethodInliningAttributes(getter);

			return getter;
		}

		/* not used yet
		private static PropertyDefinition DefineInterfaceGetSetProperty(TypeBuilder interfaceDef, Type propType, string propName) {
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
		private PropertyDefinition DefineInterfaceGetProperty(TypeDefinition interfaceDef, Type propType, string propName) {
			return DefineInterfaceGetProperty(interfaceDef, propType.Import(Module), propName);
		}

		private PropertyDefinition DefineInterfaceGetProperty(TypeDefinition interfaceDef, TypeReference propType, string propName) {
			var propDef = interfaceDef.DefineProperty(propName,
				PropertyAttributes.None,
				propType, Type.EmptyTypes);
			var propGetter = interfaceDef.DefineMethod("get_" + propName,
				InterfaceMethodAttributes,
				propType, Type.EmptyTypes);
			SetMethodInliningAttributes(propGetter);
			propDef.SetGetMethod(propGetter);
			return propDef;
		}

		private void BuildInterfaceByRefAccessor(
			TypeDefinition interfaceDef, string propName,
			ConcurrentDictionary<string,GenericInstanceType> splitPointerDefs,
			LinkedListNode<ParameterInfo> fieldInfo32,
			LinkedListNode<ParameterInfo> fieldInfo64,
			out PropertyDefinition interfacePropDef,
			out MethodDefinition interfaceMethodDef
			) {
			var module = interfaceDef.Module;

			var fieldType32 = fieldInfo32.Value.Type;
			var fieldType64 = fieldInfo64.Value.Type;

			interfacePropDef = null;
			interfaceMethodDef = null;
			
			LinkedList<TypeReferenceTransform> transforms32;
			LinkedList<TypeReferenceTransform> transforms64;

			if (fieldType32.Is(IntType) && fieldType64.Is(LongType)) {
				// IntPtr
				var propType = IntPtrType;
				var propRefType = propType.MakeByReferenceType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}
			if (fieldType32.Is(UIntType) && fieldType64.Is(ULongType)) {
				// UIntPtr
				var propType = UIntPtrType;
				var propRefType = propType.MakeByReferenceType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}

			if (fieldType32.Is(fieldType64) && fieldType32.IsDirect()) {
				// same type
				var propType = fieldType32;
				var propRefType = propType.MakeByReferenceType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}
			

			var fieldElemType32 = fieldType32.GetTypePointedTo(out transforms32);
			var fieldElemType64 = fieldType64.GetTypePointedTo(out transforms64);

			var pointerDepth32 = transforms32.Count;
			var pointerDepth64 = transforms64.Count;

			if ( Math.Abs(pointerDepth32 - pointerDepth64) > 1 )
				throw new NotSupportedException();

			if (fieldType32.IsPointer && fieldType64.Is(IntType)) {
				// 32-bit handle
				var handleType = HandleInt32Gtd.MakeGenericInstanceType(fieldElemType32);
				
				fieldInfo32.Value.Type = handleType;
				fieldInfo64.Value.Type = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}
			if (fieldType32.IsPointer && fieldType64.Is(UIntType)) {
				// 32-bit handle
				var handleType = HandleUInt32Gtd.MakeGenericInstanceType(fieldElemType32);
				
				fieldInfo32.Value.Type = handleType;
				fieldInfo64.Value.Type = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}
			if (fieldType32.Is(LongType) && fieldType64.IsPointer) {
				// 64-bit handle
				var handleType = HandleInt64Gtd.MakeGenericInstanceType(fieldElemType64);
				
				fieldInfo32.Value.Type = handleType;
				fieldInfo64.Value.Type = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}
			if (fieldType32.Is(ULongType) && fieldType64.IsPointer) {
				// 64-bit handle
				var handleType = HandleUInt64Gtd.MakeGenericInstanceType(fieldElemType64);
				
				fieldInfo32.Value.Type = handleType;
				fieldInfo64.Value.Type = handleType;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName);
				return;
			}

			if (fieldElemType64.Is(IntType) && IsHandleType(fieldElemType32)) {
				// 32-bit handle
				TypeReference handleType = HandleInt32Gtd.MakeGenericInstanceType(fieldElemType32);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.Type = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo64.Value.Type = updatedFieldType64;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms32.Skip(1));
				return;
			}
			if (fieldElemType64.Is(UIntType) && IsHandleType(fieldElemType32)) {
				// 32-bit handle
				TypeReference handleType = HandleUInt32Gtd.MakeGenericInstanceType(fieldElemType32);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.Type = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo64.Value.Type = updatedFieldType64;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms32.Skip(1));
				return;
			}
			if (fieldElemType32.Is(LongType) && IsHandleType(fieldElemType64)) {
				// 64-bit handle
				TypeReference handleType = HandleInt64Gtd.MakeGenericInstanceType(fieldElemType64);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.Type = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo32.Value.Type = updatedFieldType32;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms64.Skip(1));
				return;
			}
			if (fieldElemType32.Is(ULongType) && IsHandleType(fieldElemType64)) {
				// 64-bit handle
				TypeReference handleType = HandleUInt64Gtd.MakeGenericInstanceType(fieldElemType64);

				var updatedFieldType32 = handleType;
				foreach (var transform in transforms32)
					transform(ref updatedFieldType32);

				fieldInfo32.Value.Type = updatedFieldType32;

				var updatedFieldType64 = handleType;
				foreach (var transform in transforms64)
					transform(ref updatedFieldType64);

				fieldInfo32.Value.Type = updatedFieldType32;

				interfacePropDef =
					DefineInterfaceGetHandleProperty(interfaceDef, handleType, propName, transforms64.Skip(1));
				return;
			}

			if (fieldType32.IsPointer && fieldType64.IsPointer && fieldElemType32.Is(fieldElemType64)) {
				if (fieldElemType32.SizeOf() == 0)
					throw new NotImplementedException();

				TypeReference propRefType = fieldElemType32;
				foreach (var transform in transforms32)
					transform(ref propRefType);
				propRefType = propRefType.MakeByReferenceType();

				interfacePropDef =
					DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}

			var interiorType32 = fieldElemType32.FindInteriorType(ref transforms32);
			var interiorType64 = fieldElemType64.FindInteriorType(ref transforms64);

			if (!transforms32.SequenceEqual(transforms64)) {
				throw new NotImplementedException();
			}

			if (fieldType32.Is(fieldType64) && fieldType32.IsArray) {
				// ref index implementation

				if (interiorType32.SizeOf() == 0)
					throw new NotImplementedException();

				var fieldElemType = fieldType64.DescendElementType();

				if (IsHandleType(interiorType64)) {
					var handleType = HandleUIntPtrGtd.MakeGenericInstanceType(interiorType64);
					TypeReference handleElemType = handleType;
					foreach (var transform in transforms64.Take(transforms64.Count - 2))
						transform(ref handleElemType);
					var handleElemRefType = handleElemType.MakeByReferenceType();
					interfaceMethodDef =
						DefineInterfaceGetByIndexMethod(interfaceDef, handleElemRefType, propName);
					return;
				}


				var propElemRefType = fieldElemType.MakeByReferenceType();

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
				var propType = splitPointerDef
					?? module.ImportReference( SplitPointerGtd.MakeGenericInstanceType(commonInterface, interiorType32, interiorType64) );
				foreach (var transform in transforms32.Skip(1))
					transform(ref propType);
				var propRefType = propType.MakeByReferenceType();
				interfacePropDef = DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
				return;
			}
			throw new NotImplementedException();
		}
		
	}
}