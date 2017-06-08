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
				return DefineClrHandleStructInternal(structInfo64, 64);
			}
			if (structInfo64 == null) {
				return DefineClrHandleStructInternal(structInfo32, 32);
			}
			return DefineClrStructInternal(structInfo32, structInfo64);
		}

		private Func<Type[]> DefineClrHandleStructInternal(ClangStructInfo structInfo, int? bits = null) {
			if (structInfo.Size > 0)
				throw new NotImplementedException();
			// handle type
			var handleDef = Module.DefineType(structInfo.Name,
				PublicSealedStructTypeAttributes, null, 0, 0);
			handleDef.SetCustomAttribute(StructLayoutSequentialAttributeInfo);
			var handleInterface = typeof(IHandle<>).MakeGenericType(handleDef);
			handleDef.AddInterfaceImplementation(handleInterface);
			var handlePointerType = handleDef.MakePointerType();
			var inputTypes = bits == null
				? TypeArrayOfSingularVoidPointer
				: bits == 64
					? TypeArrayOfSingularULong
					: TypeArrayOfSingularUInt;
			var castMethod = handleDef.DefineMethod("Cast", PublicStaticMethodAttributes,
				handlePointerType, inputTypes);
			SetMethodInliningAttributes(castMethod);
			castMethod.GenerateIL(il => {
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ret);
			});
			var handleType = handleDef.CreateType();
			return () => new[] {handleType};
		}

		private readonly ConcurrentDictionary<string, Type> _splitPointerDefs
			= new ConcurrentDictionary<string, Type>();

		private static readonly ConstructorInfo ArgumentOutOfRangeCtorInfo = typeof(ArgumentOutOfRangeException).GetConstructor(Type.EmptyTypes);
		private static readonly Type[] TypeArrayOfSingularVoidPointer = {typeof(void*)};
		private static readonly Type[] TypeArrayOfSingularULong = {typeof(ulong)};
		private static readonly Type[] TypeArrayOfSingularUInt = {typeof(uint)};
		private static readonly Type FixedBufferAttributeType = typeof(FixedBufferAttribute);

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


			var fieldParams32 = new LinkedList<CustomParameterInfo>(
				structInfo32.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));
			var fieldParams64 = new LinkedList<CustomParameterInfo>(
				structInfo64.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));

			var interfacePropNames = new LinkedList<string>(structInfo32.Fields
				.Select(f => f.Name)
				.Union(structInfo64.Fields
					.Select(f => f.Name)));

			Type[] BuildInterfacePropsAndStructFields() {
				foreach (var fieldParam in fieldParams32)
					fieldParam.RequireCompleteTypes("32");
				foreach (var fieldParam in fieldParams64)
					fieldParam.RequireCompleteTypes("64");

				interfacePropNames.ConsumeLinkedList(propName => {
					BuildInterfaceByRefAccessor(interfaceDef, propName, _splitPointerDefs,
						fieldParams32.Nodes().First(f => f.Value.Name == propName),
						fieldParams64.Nodes().First(f => f.Value.Name == propName),
						out var interfacePropDef, out var interfaceMethodDef);
					if (interfacePropDef != null)
						interfacePropDefs[propName] = interfacePropDef;
					if (interfaceMethodDef != null)
						interfaceMethodDefs[propName] = interfaceMethodDef;
				});
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

					// never define an array element on a struct
					// there should be a fixed buffer attribute on the field
					if (isArray)
						fieldType = fieldType.GetElementType();

					var fieldDef = PrepareAndDefineField(
						structDef, isArray,
						fieldParam, ref fieldType,
						out var fieldInteriorType,
						out var fieldTransforms);

					Type fieldRefType;
					if (isArray) {
						fieldRefType = fieldType.MakeByRefType();

						var intfMethodInfo = interfaceType.GetMethod(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						Type intfMethodType;
						try {
							intfMethodType = intfMethodInfo.ReturnType;
						}
						catch {
							intfMethodType = interfaceMethodDefs[fieldName].ReturnType;
						}
						var intfMethodElemType = intfMethodType.GetElementType();
						var intfMethodInteriorType = GetInteriorType(intfMethodType, out var methodRetTransforms);

						if (fieldRefType == intfMethodType
							|| fieldType.IsPointer && intfMethodElemType.IsPointer
							|| IsIntPtrOrUIntPtr(intfMethodElemType) && fieldType.UnsafeSizeOf() == ptrSizeForType
							|| IsTypedHandle(intfMethodElemType, fieldType)
							|| fieldInteriorType == intfMethodInteriorType && fieldTransforms.SequenceEqual(methodRetTransforms.Skip(1))) {
							var fixedBufAttrInfo = fieldParam.AttributeInfos.First(ai => ai.Type == FixedBufferAttributeType);
							var fixedBufSize = (int) fixedBufAttrInfo.Arguments[1];
							var structGetter = structDef.DefineMethod(fieldName,
								PublicInterfaceImplementationMethodAttributes,
								intfMethodType, new[] {typeof(int)});
							structDef.DefineMethodOverride(structGetter, intfMethodInfo);
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

								il.Emit(OpCodes.Sizeof, fieldType);
								il.Emit(OpCodes.Mul);
								il.Emit(OpCodes.Add);
								if (intfMethodType.IsInterface) {
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
					fieldRefType = fieldType.MakeByRefType();

					var propInfo = interfaceType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
						|| fieldType.IsPointer && propElemType.IsPointer
						|| IsIntPtrOrUIntPtr(propElemType) && fieldType.UnsafeSizeOf() == ptrSizeForType
						|| IsTypedHandle(propElemType, fieldType)
						|| fieldInteriorType == propInteriorType && fieldTransforms.SequenceEqual(propTransforms.Skip(1))) {
						/*
						var structProp = structDef.DefineProperty(fieldName,
							PropertyAttributes.SpecialName,
							propType, Type.EmptyTypes);
						*/
						var structGetter = structDef.DefineMethod("get_" + fieldName, HiddenPropertyMethodAttributes | MethodAttributes.Virtual, propType, Type.EmptyTypes);
						SetMethodInliningAttributes(structGetter);
						//structProp.SetGetMethod(structGetter);
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
			}

			return BuildInterfacePropsAndStructFields;
		}

		private FieldBuilder PrepareAndDefineField(TypeBuilder structDef, bool isArray, CustomParameterInfo fieldParam, ref Type fieldType, out Type fieldInteriorType, out LinkedList<TypeTransform> fieldTransforms) {
			var fieldName = fieldParam.Name;
			var arrayLength = -1;
			if (isArray) {
				foreach (var ai in fieldParam.AttributeInfos) {
					if (ai.Type != FixedBufferAttributeType)
						continue;
					arrayLength = (int) ai.Arguments[1];
					ai.Arguments = new object[] {fieldType, arrayLength};
					break;
				}
			}
			if (arrayLength == 0)
				throw new NotImplementedException();

			FieldBuilder fieldDef;
			if (isArray) {
				// instead of building a structure and following fixed buffer quirks,
				// just unroll the would-be fixed buffer structure ...
				// define all of the fields, let the interface ref accessor property handle visibility

				var fieldParamAttributeInfos = fieldParam.AttributeInfos
					.Where(ai => ai.Type != FixedBufferAttributeType)
					.ToArray();

				fieldDef = structDef.DefineField($"{fieldName}[0]", fieldType, FieldAttributes.PrivateScope);
				foreach (var attr in fieldParamAttributeInfos)
					fieldDef.SetCustomAttribute(attr);

				for (var i = 1 ; i < arrayLength ; ++i) {
					var subFieldDef = structDef.DefineField($"{fieldName}[{i}]", fieldType, FieldAttributes.PrivateScope);
					foreach (var attr in fieldParamAttributeInfos)
						subFieldDef.SetCustomAttribute(attr);
				}
			}
			else {
				fieldDef = structDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
			}


			fieldInteriorType = GetInteriorType(fieldType, out fieldTransforms);

			if ((fieldInteriorType as TypeBuilder)?.IsCreated() ?? false) {
				if (fieldInteriorType.IsDirect())
					fieldInteriorType = Module.ResolveType(Module.GetTypeToken(fieldInteriorType).Token);

				if (IsHandleType(fieldInteriorType))
					throw new NotImplementedException();

				var rebuiltFieldType = fieldInteriorType;
				foreach (var transform in fieldTransforms)
					transform(ref rebuiltFieldType);

				fieldType = rebuiltFieldType;
			}

			return fieldDef;
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