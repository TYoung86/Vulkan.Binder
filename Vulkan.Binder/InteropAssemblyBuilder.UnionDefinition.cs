using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Interop;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<TypeDefinition[]> DefineClrType(ClangUnionInfo unionInfo) {
			if (unionInfo.Size == 0) {
				throw new NotImplementedException();
			}
			var unionName = unionInfo.Name;
			Debug.WriteLine($"Defining union {unionName}");
			if (TypeRedirects.TryGetValue(unionName, out var rename)) {
				unionName = rename;
			}
			if (Module.GetType(unionName)?.Resolve() != null)
				return null;
			TypeDefinition unionDef = Module.DefineType(unionName,
				PublicSealedUnionTypeAttributes, null,
				(int) unionInfo.Alignment,
				(int) unionInfo.Size);
			unionDef.SetCustomAttribute(() => new BinderGeneratedAttribute());
			//unionDef.SetCustomAttribute(StructLayoutExplicitAttributeInfo);
			var fieldParams = new LinkedList<ParameterInfo>(unionInfo.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));

			return () => {
				foreach (var fieldParam in fieldParams)
					fieldParam.Complete(TypeRedirects,true);
				
				Debug.WriteLine($"Completed dependencies for union {unionName}");

				fieldParams.ConsumeLinkedList(fieldParam => {
					var fieldName = fieldParam.Name;
					var fieldType = fieldParam.Type;
					if (!fieldType.IsArray) {
						var fieldDef = unionDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
						//fieldDef.SetCustomAttribute(AttributeInfo.Create(
						//	() => new FieldOffsetAttribute(fieldParam.Position)), Module);
						fieldDef.Offset = fieldParam.Position;
					}
					else {
						var arraySize = fieldParam.ArraySize;
						fieldType = fieldType.DescendElementType();
						var offsetPer = fieldType.SizeOf();
						if ( offsetPer == -1 )
							throw new NotImplementedException();
						var fieldDef = unionDef.DefineField($"{fieldName}[0]",
							fieldType, FieldAttributes.Private);
						fieldDef.Offset = fieldParam.Position;
						for (var i = 1 ; i < arraySize ; ++i) {
							unionDef.DefineField($"{fieldName}[{i}]",
								fieldType, FieldAttributes.Private)
							.Offset = fieldParam.Position + i * offsetPer;
						}

						var unionGetter = unionDef.DefineMethod(fieldName,
							PublicInterfaceImplementationMethodAttributes,
							fieldType, typeof(int));

						unionGetter.DefineParameter(1, ParameterAttributes.In, "index");
						SetMethodInliningAttributes(unionGetter);
						unionGetter.GenerateIL(il => {
							var argOutOfRange = default(CecilLabel);

							if (EmitBoundsChecks) {
								argOutOfRange = il.DefineLabel();
								il.Emit(OpCodes.Ldarg_1); // index
								il.EmitPushConst(0); // underflow
								il.Emit(OpCodes.Blt, argOutOfRange);

								il.Emit(OpCodes.Ldarg_1); // index
								il.EmitPushConst(arraySize); // overflow
								il.Emit(OpCodes.Bge, argOutOfRange);
							}

							il.Emit(OpCodes.Ldarg_0); // this
							il.Emit(OpCodes.Ldflda, fieldDef);
							il.Emit(OpCodes.Ldarg_1); // index

							il.Emit(OpCodes.Sizeof, fieldType);
							il.Emit(OpCodes.Mul);
							il.Emit(OpCodes.Add);
							if (fieldType.Resolve().IsInterface) {
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
					}
				});

				return new[] {unionDef.CreateType()};
			};
		}
	}
}