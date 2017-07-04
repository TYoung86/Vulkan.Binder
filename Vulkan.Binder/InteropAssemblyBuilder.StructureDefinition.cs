using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Interop;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Vulkan.Binder.Extensions;
using SConstructorInfo = System.Reflection.ConstructorInfo;
using TypeReferenceTransform = Vulkan.Binder.Extensions.CecilExtensions.TypeReferenceTransform;
using BindingFlags = System.Reflection.BindingFlags;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<TypeDefinition[]> DefineClrType(ClangStructInfo structInfo32, ClangStructInfo structInfo64) {
			if (structInfo32 == null) {
				return DefineClrHandleStructInternal(structInfo64, 64);
			}
			if (structInfo64 == null) {
				return DefineClrHandleStructInternal(structInfo32, 32);
			}
			return DefineClrStructInternal(structInfo32, structInfo64);
		}

		private Func<TypeDefinition[]> DefineClrHandleStructInternal(ClangStructInfo structInfo, int? bits = null) {
			if (structInfo.Size > 0)
				throw new NotImplementedException();
			var structName = structInfo.Name;

			if (TypeRedirects.TryGetValue(structName, out var rename)) {
				structName = rename;
			}
			if (Module.GetType(structName)?.Resolve() != null)
				return null;
			// handle type
			var handleDef = Module.DefineType(structName,
				PublicSealedStructTypeAttributes);
			handleDef.SetCustomAttribute(() => new BinderGeneratedAttribute());
			//handleDef.SetCustomAttribute(StructLayoutSequentialAttributeInfo);
			var handleInterface = IHandleGtd.MakeGenericInstanceType(handleDef);
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

		private readonly ConcurrentDictionary<string, GenericInstanceType> _splitPointerDefs
			= new ConcurrentDictionary<string, GenericInstanceType>();

		private static readonly SConstructorInfo ArgumentOutOfRangeCtorInfo
			= typeof(ArgumentOutOfRangeException).GetTypeInfo().GetConstructor(Type.EmptyTypes);


		public readonly MethodReference ArgumentOutOfRangeCtor;

		// TODO: convert to TypeReferences
		private static readonly Type[] TypeArrayOfSingularVoidPointer = {typeof(void*)};
		private static readonly Type[] TypeArrayOfSingularULong = {typeof(ulong)};
		private static readonly Type[] TypeArrayOfSingularUInt = {typeof(uint)};

		private Func<TypeDefinition[]> DefineClrStructInternal(ClangStructInfo structInfo32, ClangStructInfo structInfo64) {
			if (structInfo32.Size == 0 && structInfo64.Size == 0) {
				return DefineClrHandleStructInternal(structInfo64);
			}

			var structName = structInfo32.Name;

			Debug.WriteLine($"Defining interface and structure {structName}");
			
			if (TypeRedirects.TryGetValue(structName, out var rename)) {
				structName = rename;
			}
			var interfaceName = "I" + structName;
			if (Module.GetType(interfaceName)?.Resolve() != null)
				return null;

			var interfaceDef = Module.DefineType(interfaceName,
				PublicInterfaceTypeAttributes);
			interfaceDef.SetCustomAttribute(() => new BinderGeneratedAttribute());

			var structDef32 = Module.DefineType(structName + "32",
				PublicSealedStructTypeAttributes, null,
				(int) structInfo32.Alignment,
				(int) structInfo32.Size);
			structDef32.SetCustomAttribute(() => new BinderGeneratedAttribute());
			/*
			structDef32.SetCustomAttribute(AttributeInfo.Create(
				() => new StructLayoutAttribute(LayoutKind.Sequential) {
					Pack = (int) structInfo32.Alignment,
					Size = (int) structInfo32.Size
				}));
			*/
			var structDef64 = Module.DefineType(structName + "64",
				PublicSealedStructTypeAttributes, null,
				(int) structInfo64.Alignment,
				(int) structInfo64.Size);
			structDef64.SetCustomAttribute(() => new BinderGeneratedAttribute());
			/*
			structDef64.SetCustomAttribute(AttributeInfo.Create(
				() => new StructLayoutAttribute(LayoutKind.Sequential) {
					Pack = (int) structInfo64.Alignment,
					Size = (int) structInfo64.Size
				}));
			*/
			_splitPointerDefs[interfaceDef.FullName] = SplitPointerGtd
				.MakeGenericInstanceType(interfaceDef, structDef32, structDef64);


			if (!structDef32.Interfaces.Contains(interfaceDef))
				structDef32.AddInterfaceImplementation(interfaceDef);

			if (!structDef64.Interfaces.Contains(interfaceDef))
				structDef64.AddInterfaceImplementation(interfaceDef);

			var interfacePropDefs = new ConcurrentDictionary<string, PropertyDefinition>();
			var interfaceMethodDefs = new ConcurrentDictionary<string, MethodDefinition>();


			var fieldParams32 = new LinkedList<ParameterInfo>(
				structInfo32.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));
			var fieldParams64 = new LinkedList<ParameterInfo>(
				structInfo64.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));

			var interfacePropNames = new LinkedList<string>(structInfo32.Fields
				.Select(f => f.Name)
				.Union(structInfo64.Fields
					.Select(f => f.Name)));

			TypeDefinition[] BuildInterfacePropsAndStructFields() {
				foreach (var fieldParam in fieldParams32)
					fieldParam.Complete(TypeRedirects, "32");
				foreach (var fieldParam in fieldParams64)
					fieldParam.Complete(TypeRedirects, "64");

				Debug.WriteLine($"Completed dependencies for interface and structure {structName}");

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

				void BuildStructField(ParameterInfo fieldParam, TypeDefinition structDef, int bits) {
					var ptrSizeForType = bits / 8;

					var fieldName = fieldParam.Name;
					var fieldType = fieldParam.Type;

					var isArray = fieldType.IsArray;

					// never define an array element on a struct
					// there should be a fixed buffer attribute on the field
					if (isArray)
						fieldType = fieldType.DescendElementType();

					var fieldDef = PrepareAndDefineField(
						structDef, isArray,
						fieldParam, ref fieldType,
						out var fieldInteriorType,
						out var fieldTransforms);

					TypeReference fieldRefType;
					if (isArray) {
						fieldRefType = fieldType.MakeByReferenceType();

						var intfMethodInfo = interfaceType.GetMethod(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						TypeReference intfMethodType;
						try {
							intfMethodType = intfMethodInfo.ReturnType;
						}
						catch {
							intfMethodType = interfaceMethodDefs[fieldName].ReturnType;
						}
						var intfMethodElemType = intfMethodType.DescendElementType();
						var intfMethodInteriorType = intfMethodType.GetInteriorType(out var methodRetTransforms);

						if (fieldRefType.Is(intfMethodType)
							|| fieldType.IsPointer && intfMethodElemType.IsPointer
							|| IsIntPtrOrUIntPtr(intfMethodElemType) && fieldType.SizeOf(ptrSizeForType) == ptrSizeForType
							|| IsTypedHandle(intfMethodElemType, fieldType)
							|| fieldInteriorType.Is(intfMethodInteriorType) && fieldTransforms.SequenceEqual(methodRetTransforms.Skip(1))) {
							//var fixedBufAttrInfo = fieldParam.AttributeInfos.First(ai => ai.Type == FixedBufferAttributeType);
							var fixedBufSize = fieldParam.ArraySize;
							var structGetter = structDef.DefineMethod(fieldName,
								PublicInterfaceImplementationMethodAttributes,
								intfMethodType, typeof(int));
							structDef.DefineMethodOverride(structGetter, intfMethodInfo);
							structGetter.DefineParameter(1, ParameterAttributes.In, "index");
							SetMethodInliningAttributes(structGetter);
							structGetter.GenerateIL(il => {
								var argOutOfRange = default(CecilLabel);

								if (EmitBoundsChecks) {
									argOutOfRange = il.DefineLabel();
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
								if (intfMethodType.Resolve().IsInterface) {
									il.Emit(OpCodes.Box, fieldType);
								}
								il.Emit(OpCodes.Ret);

								if (EmitBoundsChecks) {
									il.MarkLabel(argOutOfRange);
									il.Emit(OpCodes.Newobj, ArgumentOutOfRangeCtor);
									il.Emit(OpCodes.Throw);
									// ReSharper disable once PossibleNullReferenceException
									argOutOfRange.Cleanup();
								}
							});
							return;
						}

						throw new NotImplementedException();
					}
					fieldRefType = fieldType.MakeByReferenceType();

					var propInfo = interfaceType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (propInfo == null)
						throw new NotImplementedException();
					TypeReference propType;
					try {
						propType = propInfo.PropertyType;
					}
					catch {
						propType = interfacePropDefs[fieldName].PropertyType;
					}
					var propInteriorType = propType.GetInteriorType(out var propTransforms).Resolve();
					if ( propInteriorType == null )
						throw new NotImplementedException();


					var propElemType = propType.DescendElementType();

					if (fieldRefType.Is(propType)
						|| fieldType.IsPointer && propElemType.IsPointer
						|| IsIntPtrOrUIntPtr(propElemType) && fieldType.SizeOf(ptrSizeForType) == ptrSizeForType
						|| IsTypedHandle(propElemType, fieldType)
						|| fieldInteriorType.Is(propInteriorType) && fieldTransforms.SequenceEqual(propTransforms.Skip(1))) {
						/*
						var structProp = structDef.DefineProperty(fieldName,
							PropertyAttributes.SpecialName,
							propType, Type.EmptyTypes);
						*/
						var structGetter = structDef.DefineMethod("get_" + fieldName, HiddenPropertyMethodAttributes | MethodAttributes.Virtual, propType, Type.EmptyTypes);
						SetMethodInliningAttributes(structGetter);
						//structProp.SetGetMethod(structGetter);
						structDef.DefineMethodOverride(structGetter, propInfo.Resolve().GetMethod);
						structGetter.GenerateIL(il => {
							il.Emit(OpCodes.Ldarg_0); // this
							il.Emit(OpCodes.Ldflda, fieldDef);
							if (propInteriorType.IsInterface) {
								il.Emit(OpCodes.Box, fieldType);
							}
							il.Emit(OpCodes.Ret);
						});
						return;
					}

					if (propType.Is(ObjectType)) {
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

				var structType32 = structDef32;

				fieldParams64.ConsumeLinkedList(fieldParam => BuildStructField(fieldParam, structDef64, 64));

				var structType64 = structDef64;

				return new[] {interfaceType, structType32, structType64};
			}

			return BuildInterfacePropsAndStructFields;
		}

		private FieldDefinition PrepareAndDefineField(TypeDefinition structDef, bool isArray, ParameterInfo fieldParam, ref TypeReference fieldType, out TypeReference fieldInteriorType, out LinkedList<TypeReferenceTransform> fieldTransforms) {
			var fieldName = fieldParam.Name;
			var arrayLength = fieldParam.ArraySize;
			if (arrayLength == 0)
				throw new NotImplementedException();

			FieldDefinition fieldDef;
			if (isArray) {
				// instead of building a structure and following fixed buffer quirks,
				// just unroll the would-be fixed buffer structure ...
				// define all of the fields, let the interface ref accessor property handle visibility

				fieldDef = structDef.DefineField($"{fieldName}[0]", fieldType, FieldAttributes.Private);

				for (var i = 1 ; i < arrayLength ; ++i) {
					var subFieldDef = structDef.DefineField($"{fieldName}[{i}]", fieldType, FieldAttributes.Private);
				}
			}
			else {
				fieldDef = structDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
			}


			fieldInteriorType = fieldType.GetInteriorType(out fieldTransforms);

			if ((fieldInteriorType as TypeDefinition)?.IsCreated() ?? false) {
				//if (fieldInteriorType.IsDirect()) {
					//fieldInteriorType = Module.GetType(fieldInteriorType.FullName);
					//Module.ResolveType(Module.GetTypeToken(fieldInteriorType).Token);
				//}

				//if (IsHandleType(fieldInteriorType))
				//	throw new NotImplementedException();

				var rebuiltFieldType = fieldInteriorType;
				foreach (var transform in fieldTransforms)
					transform(ref rebuiltFieldType);

				fieldType = rebuiltFieldType;
			}

			return fieldDef;
		}

		private Func<TypeDefinition[]> DefineClrType(ClangStructInfo structInfo) {
			if (structInfo.Size == 0) {
				return DefineClrHandleStructInternal(structInfo);
			}
			var structName = structInfo.Name;
			Debug.WriteLine($"Defining simple structure {structName}");
			if (TypeRedirects.TryGetValue(structName, out var rename)) {
				structName = rename;
			}
			if (Module.GetType(structName)?.Resolve() != null)
				return null;
			TypeDefinition structDef = Module.DefineType(structName,
				PublicSealedStructTypeAttributes, null,
				(int) structInfo.Alignment,
				(int) structInfo.Size);
			structDef.SetCustomAttribute(() => new BinderGeneratedAttribute());

			var fieldParams = new LinkedList<ParameterInfo>(structInfo.Fields
				.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));
			return () => {
				foreach (var fieldParam in fieldParams)
					fieldParam.Complete(TypeRedirects,true);
				Debug.WriteLine($"Completed dependencies for simple structure {structName}");
				fieldParams.ConsumeLinkedList(fieldParam => {
					var fieldName = fieldParam.Name;

					var fieldType = fieldParam.Type;

					var fieldDef = structDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
				});
				return new[] {
					structDef.CreateType()
				};
			};
		}
	}
}