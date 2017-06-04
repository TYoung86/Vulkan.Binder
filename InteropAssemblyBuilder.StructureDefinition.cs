using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<Type[]> DefineClrType(ClangStructInfo structInfo32, ClangStructInfo structInfo64) {
			if (structInfo32 == null) {
				return DefineClrHandleStructInternal(structInfo64);
			}
			if (structInfo64 == null) {
				return DefineClrHandleStructInternal(structInfo32);
			}
			return DefineClrStructInternal(structInfo32, structInfo64);
		}

		private Func<Type[]> DefineClrHandleStructInternal(ClangStructInfo structInfo) {
			if (structInfo.Size > 0)
				throw new NotImplementedException();
			// handle type
			var handleDef = Module.DefineType(structInfo.Name,
				PublicSealedStructTypeAttributes, null, 0, 0);
			handleDef.SetCustomAttribute(StructLayoutSequentialAttributeInfo);
			var handleInterface = typeof(IHandle<>).MakeGenericType(handleDef);
			handleDef.AddInterfaceImplementation(handleInterface);
			var handleType = handleDef.CreateType();
			return () => new[] {handleType};
		}

		private readonly ConcurrentDictionary<string, Type> _splitPointerDefs
			= new ConcurrentDictionary<string, Type>();

		private static readonly ConstructorInfo ArgumentOutOfRangeCtorInfo = typeof(ArgumentOutOfRangeException).GetConstructor(Type.EmptyTypes);

		private Func<Type[]> DefineClrStructInternal(ClangStructInfo structInfo32, ClangStructInfo structInfo64) {
			if (structInfo32.Size == 0 && structInfo64.Size == 0) {
				return DefineClrHandleStructInternal(structInfo64);
			}

			var structName = structInfo32.Name;
			var interfaceDef = Module.DefineType("I" + structName,
				PublicInterfaceTypeAttributes);

			var structDef32 = Module.DefineType(structName + "32",
				PublicSealedStructTypeAttributes, null,
				(PackingSize) structInfo32.Alignment,
				(int) structInfo32.Size);
			structDef32.SetCustomAttribute(AttributeInfo.Create(
				() => new StructLayoutAttribute(LayoutKind.Sequential) {
					Pack = (int) structInfo32.Alignment,
					Size = (int) structInfo32.Size
				}));

			var structDef64 = Module.DefineType(structName + "64",
				PublicSealedStructTypeAttributes, null,
				(PackingSize) structInfo64.Alignment,
				(int) structInfo64.Size);
			structDef64.SetCustomAttribute(AttributeInfo.Create(
				() => new StructLayoutAttribute(LayoutKind.Sequential) {
					Pack = (int) structInfo64.Alignment,
					Size = (int) structInfo64.Size
				}));

			_splitPointerDefs[interfaceDef.FullName] = typeof(SplitPointer<,,>)
				.MakeGenericType(interfaceDef, structDef32, structDef64);


			if (!structDef32.ImplementedInterfaces.Contains(interfaceDef))
				structDef32.AddInterfaceImplementation(interfaceDef);

			if (!structDef64.ImplementedInterfaces.Contains(interfaceDef))
				structDef64.AddInterfaceImplementation(interfaceDef);

			var interfacePropDefs = new ConcurrentDictionary<string, PropertyBuilder>();
			var interfaceMethodDefs = new ConcurrentDictionary<string, MethodBuilder>();


			var fieldParams32 = new LinkedList<CustomParameterInfo>(structInfo32.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));
			var fieldParams64 = new LinkedList<CustomParameterInfo>(structInfo64.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));

			var interfacePropNames = new LinkedList<string>(structInfo32.Fields
				.Select(f => f.Name)
				.Union(structInfo64.Fields
					.Select(f => f.Name)));

			return () => {
				foreach (var fieldParam in fieldParams32)
					fieldParam.RequireCompleteTypes("32");
				foreach (var fieldParam in fieldParams64)
					fieldParam.RequireCompleteTypes("64");

				// TODO: all pointers in structs should be handles
				void BuildInterfaceField(string propName) {
					var fieldInfo32 = fieldParams32.First(f => f.Name == propName);
					var fieldInfo64 = fieldParams64.First(f => f.Name == propName);
					var fieldType32 = fieldInfo32.ParameterType;
					var fieldType64 = fieldInfo64.ParameterType;


					if (fieldType32 == typeof(int) && fieldType64 == typeof(long)) {
						// IntPtr
						var propType = typeof(IntPtr);
						var propRefType = propType.MakeByRefType();

						interfacePropDefs[propName] =
							DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
						return;
					}
					if (fieldType32 == typeof(uint) && fieldType64 == typeof(ulong)) {
						// UIntPtr
						var propType = typeof(UIntPtr);
						var propRefType = propType.MakeByRefType();

						interfacePropDefs[propName] =
							DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
						return;
					}

					if (fieldType32 == fieldType64 && fieldType32.IsDirect()) {
						// same type
						var propType = fieldType32;
						var propRefType = propType.MakeByRefType();

						interfacePropDefs[propName] =
							DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
						return;
					}
					if (fieldType32.IsPointer && fieldType64 == typeof(int)) {
						// 32-bit handle
						var handleType = typeof(HandleInt<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldType32.GetElementType(), handleType, propName);
						return;
					}
					if (fieldType32.IsPointer && fieldType64 == typeof(uint)) {
						// 32-bit handle
						var handleType = typeof(HandleUInt<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldType32.GetElementType(), handleType, propName);
						return;
					}
					if (fieldType32 == typeof(long) && fieldType64.IsPointer) {
						// 64-bit handle
						var handleType = typeof(HandleLong<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldType64.GetElementType(), handleType, propName);
						return;
					}
					if (fieldType32 == typeof(ulong) && fieldType64.IsPointer) {
						// 64-bit handle
						var handleType = typeof(HandleULong<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldType64.GetElementType(), handleType, propName);
						return;
					}

					var fieldElemType32 = GetTypePointedTo(fieldType32, out var transform32);
					var fieldElemType64 = GetTypePointedTo(fieldType64, out var transform64);

					//var pointerDepth32 = transform32.Count;
					//var pointerDepth64 = transform64.Count;

					if (fieldElemType64 == typeof(int) && IsHandleType(fieldElemType32)) {
						// 32-bit handle
						var handleType = typeof(HandleInt<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType32, handleType, propName, transform32.Skip(1));
						return;
					}
					if (fieldElemType64 == typeof(uint) && IsHandleType(fieldElemType32)) {
						// 32-bit handle
						var handleType = typeof(HandleUInt<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType32, handleType, propName, transform32.Skip(1));
						return;
					}
					if (fieldElemType32 == typeof(long) && IsHandleType(fieldElemType64)) {
						// 64-bit handle
						var handleType = typeof(HandleLong<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType64, handleType, propName, transform64.Skip(1));
						return;
					}
					if (fieldElemType32 == typeof(ulong) && IsHandleType(fieldElemType64)) {
						// 64-bit handle
						var handleType = typeof(HandleULong<>);

						interfacePropDefs[propName] =
							DefineInterfaceGetHandleProperty(interfaceDef, fieldElemType64, handleType, propName, transform64.Skip(1));
						return;
					}


					if (fieldType32.IsPointer && fieldType64.IsPointer && fieldElemType32 == fieldElemType64) {
						var propRefType = fieldElemType32;
						foreach (var transform in transform32)
							transform(ref propRefType);
						propRefType = propRefType.MakeByRefType();

						interfacePropDefs[propName] =
							DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
						return;
					}

					var interiorType32 = FindInteriorType(fieldElemType32, ref transform32);
					var interiorType64 = FindInteriorType(fieldElemType64, ref transform64);

					if (!transform32.SequenceEqual(transform64)) {
						throw new NotImplementedException();
					}

					if (fieldType32 == fieldType64 && fieldType32.IsArray) {
						// ref index implementation
						var propElemRefType = fieldType64.GetElementType().MakeByRefType();

						//interfacePropDefs[propName] =
						//	DefineInterfaceGetIndexProperty(interfaceDef, propElemRefType, propName);
						interfaceMethodDefs[propName] =
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

					if (transform32.First() == MakePointerType) {
						// common interface, boxing reference
						var commonInterface = commonInterfaces.First();
						_splitPointerDefs.TryGetValue(commonInterface.FullName, out var splitPointerDef);
						var propType = splitPointerDef ?? typeof(SplitPointer<,,>)
											.MakeGenericType(commonInterface, interiorType32, interiorType64);
						foreach (var transform in transform32.Skip(1))
							transform(ref propType);
						var propRefType = propType.MakeByRefType();
						interfacePropDefs[propName] =
							DefineInterfaceGetProperty(interfaceDef, propRefType, propName);
						return;
					}
					throw new NotImplementedException();
				}

				interfacePropNames.ConsumeLinkedList(BuildInterfaceField);
				var interfaceType = interfaceDef.IsCreated()
					? Module.GetType(interfaceDef.FullName)
					: interfaceDef.CreateType();
				/*
				if (!structDef32.ImplementedInterfaces.Contains(interfaceType))
					structDef32.AddInterfaceImplementation(interfaceType);
				if (!structDef64.ImplementedInterfaces.Contains(interfaceType))
					structDef64.AddInterfaceImplementation(interfaceType);
				*/

				void BuildStructField(CustomParameterInfo fieldParam, TypeBuilder structDef, int bits) {
					var ptrSizeForType = bits / 8;
					var fieldName = fieldParam.Name;
					var fieldType = fieldParam.ParameterType;
					if (fieldType is IncompleteType)
						throw new InvalidProgramException("Encountered incomplete type in structure field definition.");

					var isArray = fieldType.IsArray;

					var fieldDef = structDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
					foreach (var attr in fieldParam.AttributeInfos)
						fieldDef.SetCustomAttribute(attr);


					var fieldInteriorType = GetInteriorType(fieldType, out var fieldTransforms);
					if ((fieldInteriorType as TypeBuilder)?.IsCreated() ?? false) {
						if (fieldInteriorType.IsDirect())
							fieldInteriorType = Module.ResolveType(Module.GetTypeToken(fieldInteriorType).Token);

						var rebuiltFieldType = fieldInteriorType;
						foreach (var transform in fieldTransforms)
							transform(ref rebuiltFieldType);
						fieldType = rebuiltFieldType;
					}
					Type fieldElemType, fieldRefType;
					if (isArray) {
						fieldElemType = fieldType.GetElementType();
						fieldRefType = fieldElemType.MakeByRefType();

						var methodInfo = interfaceType.GetMethod(fieldName);
						Type methodRetType;
						try {
							methodRetType = methodInfo.ReturnType;
						}
						catch {
							methodRetType = interfaceMethodDefs[fieldName].ReturnType;
						}
						var methodRetElemType = methodRetType.GetElementType();
						var methodRetInteriorType = GetInteriorType(methodRetType, out var methodRetTransforms);


						if (fieldRefType == methodRetType
							|| fieldElemType.IsPointer && methodRetElemType.IsPointer
							|| IsIntPtrOrUIntPtr(methodRetElemType) && fieldElemType.UnsafeSizeOf() == ptrSizeForType
							|| IsTypedHandle(methodRetElemType, fieldElemType)
							|| fieldInteriorType == methodRetInteriorType && fieldTransforms.SequenceEqual(methodRetTransforms.Skip(1))) {
							var fixedBufAttrInfo = fieldParam.AttributeInfos
								.First(ai => ai.Type == typeof(FixedBufferAttribute));
							var fixedBufSize = (int) fixedBufAttrInfo.Arguments[1];
							var structGetter = structDef.DefineMethod(fieldName,
								PropertyMethodAttributes | MethodAttributes.HideBySig | MethodAttributes.Virtual,
								methodRetType, new[] {typeof(int)});
							structGetter.DefineParameter(1, ParameterAttributes.In, "index");
							SetMethodInliningAttributes(structGetter);
							structGetter.GenerateIL(il => {
								var argOutOfRange = EmitBoundsChecks ? il.DefineLabel() : default(Label);

								if (EmitBoundsChecks) {
									il.Emit(OpCodes.Ldarg_1); // index
									il.EmitPushConst(0); // underflow
									il.Emit(OpCodes.Blt, argOutOfRange);

									il.Emit(OpCodes.Ldarg_1); // index
									il.EmitPushConst(fixedBufSize); // overflow
									il.Emit(OpCodes.Bge, argOutOfRange);
								}

								il.Emit(OpCodes.Ldarg_0); // this
								il.Emit(OpCodes.Ldflda, fieldDef);
								il.Emit(OpCodes.Ldarg_1); // index

								il.Emit(OpCodes.Sizeof, fieldElemType);
								il.Emit(OpCodes.Mul);
								il.Emit(OpCodes.Add);
								if (methodRetType.IsInterface) {
									il.Emit(OpCodes.Box, fieldType);
								}
								il.Emit(OpCodes.Ret);

								if (EmitBoundsChecks) {
									il.MarkLabel(argOutOfRange);
									il.Emit(OpCodes.Newobj, ArgumentOutOfRangeCtorInfo);
									il.Emit(OpCodes.Throw);
								}
							});
							return;
						}
						
						throw new NotImplementedException();
					}
					fieldElemType = fieldType;
					fieldRefType = fieldElemType.MakeByRefType();

					var propInfo = interfaceType.GetProperty(fieldName);
					if (propInfo == null)
						throw new NotImplementedException();
					Type propType;
					try {
						propType = propInfo.PropertyType;
					}
					catch {
						propType = interfacePropDefs[fieldName].PropertyType;
					}
					var propInteriorType = GetInteriorType(propType, out var propTransforms);


					var propElemType = propType.GetElementType();

					if (fieldRefType == propType
						|| fieldElemType.IsPointer && propElemType.IsPointer
						|| IsIntPtrOrUIntPtr(propElemType) && fieldElemType.UnsafeSizeOf() == ptrSizeForType
						|| IsTypedHandle(propElemType, fieldElemType)
						|| fieldInteriorType == propInteriorType && fieldTransforms.SequenceEqual(propTransforms.Skip(1))) {
						var structProp = structDef.DefineProperty(fieldName, PropertyAttributes.SpecialName, propType, Type.EmptyTypes);
						var structGetter = structDef.DefineMethod("get_" + fieldName,
							PropertyMethodAttributes | MethodAttributes.HideBySig | MethodAttributes.Virtual,
							propType, Type.EmptyTypes);
						SetMethodInliningAttributes(structGetter);
						structProp.SetGetMethod(structGetter);
						structDef.DefineMethodOverride(structGetter, propInfo.GetMethod);
						structGetter.GenerateIL(il => {
							il.Emit(OpCodes.Ldarg_0); // this
							il.Emit(OpCodes.Ldflda, fieldDef);
							if (propType.IsInterface) {
								il.Emit(OpCodes.Box, fieldType);
							}
							il.Emit(OpCodes.Ret);
						});
						return;
					}

					if (propType == typeof(object)) {
						// TODO: boxing reference
						throw new NotImplementedException();
					}
					if (propType.IsAssignableFrom(fieldType)) {
						// TODO: boxed interface
						throw new NotImplementedException();
					}
					throw new NotImplementedException();
				}

				fieldParams32.ConsumeLinkedList(fieldParam => BuildStructField(fieldParam, structDef32, 32));

				var structType32 = structDef32.IsCreated()
					? Module.GetType(structDef32.FullName)
					: structDef32.CreateType();

				fieldParams64.ConsumeLinkedList(fieldParam => BuildStructField(fieldParam, structDef64, 64));

				var structType64 = structDef64.IsCreated()
					? Module.GetType(structDef64.FullName)
					: structDef64.CreateType();

				return new[] {interfaceType, structType32, structType64};
			};
		}

		private Func<Type[]> DefineClrType(ClangStructInfo structInfo) {
			if (structInfo.Size == 0) {
				return DefineClrHandleStructInternal(structInfo);
			}
			TypeBuilder structDef = Module.DefineType(structInfo.Name,
				PublicSealedStructTypeAttributes, null,
				(PackingSize) structInfo.Alignment,
				(int) structInfo.Size);

			var fieldParams = new LinkedList<CustomParameterInfo>(structInfo.Fields
				.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));
			return () => {
				foreach (var fieldParam in fieldParams)
					fieldParam.RequireCompleteTypes(true);
				fieldParams.ConsumeLinkedList(fieldParam => {
					var fieldName = fieldParam.Name;

					var fieldType = fieldParam.ParameterType;
					if (fieldType is IncompleteType)
						throw new InvalidProgramException("Encountered incomplete type in structure field definition.");

					var fieldDef = structDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
					foreach (var attr in fieldParam.AttributeInfos)
						fieldDef.SetCustomAttribute(attr);
				});
				return new[] {
					structDef.CreateType()
				};
			};
		}
	}
}