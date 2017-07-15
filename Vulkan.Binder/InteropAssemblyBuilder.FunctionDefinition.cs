using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Interop;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {

		private readonly TypeReference MarshalTypeRef;

		private readonly MethodReference GetDelegateForFpMethodGtd;

		private readonly MethodReference GetFpForDelegateMethodGtd;

		private Func<TypeDefinition[]> DefineClrType(ClangFunctionInfoBase funcInfo32, ClangFunctionInfoBase funcInfo64) {
			if (funcInfo32 == funcInfo64) {
				return DefineClrType(funcInfo64);
			}

			throw new NotImplementedException();
		}

		private Func<TypeDefinition[]> DefineClrType(ClangFunctionInfoBase funcInfo) {
			var funcName = funcInfo.Name;

			Debug.WriteLine($"Defining function {funcName}");

			if (TypeRedirects.TryGetValue(funcName, out var rename)) {
				funcName = rename;
			}

			var funcRef = Module.GetType(funcName, true);
			var funcDef = funcRef.Resolve();

			if (funcDef != null)
				return null;

			funcDef = Module.DefineType(funcName,
				DelegateTypeAttributes,
				MulticastDelegateType);
			funcDef.SetCustomAttribute(() => new BinderGeneratedAttribute());

			var umfpDef = new TypeDefinition(funcDef.Namespace, funcDef.Name + "Unmanaged",
				TypeAttributes.Sealed
				| TypeAttributes.BeforeFieldInit
				| TypeAttributes.SequentialLayout
				| TypeAttributes.Public,
				Module.TypeSystem.ValueType);
			Module.Types.Add(umfpDef);

			// todo: add implicit conversion ops using Marshal

			var retParam = ResolveParameter(funcInfo.ReturnType);
			/* todo: figure out why the attribute is jacked up
			if (!CallingConventionMap.TryGetValue(funcInfo.CallConvention, out var callConv))
				throw new NotImplementedException();
			if (!ClrCallingConventionAttributeMap.TryGetValue(callConv, out var callConvAttr))
				throw new NotImplementedException();
			*/
			var argParams = new LinkedList<ParameterInfo>(funcInfo.Parameters.Select(p => ResolveParameter(p.Type, p.Name, (int) p.Index)));

			return () => {
				retParam.Complete(TypeRedirects, true);

				var retType = retParam.Type;

				foreach (var argParam in argParams)
					argParam.Complete(TypeRedirects, true);

				Debug.WriteLine($"Completed dependencies for function {funcName}");

				var umfpRef = Module.ImportReference(umfpDef);

				var umfpValue = umfpDef.DefineField("Value", Module.TypeSystem.IntPtr,
					FieldAttributes.Public
					| FieldAttributes.InitOnly);

				var getDelegateForFpMethodDef = Module.ImportReference(
					new GenericInstanceMethod(GetDelegateForFpMethodGtd) {
						GenericArguments = {funcDef}
					});

				var getFpForDelegateMethodDef = Module.ImportReference(
					new GenericInstanceMethod(GetFpForDelegateMethodGtd) {
						GenericArguments = {funcDef}
					});

				var umfpDlgt = umfpDef.DefineMethod("op_Implicit",
					PublicStaticMethodAttributes | MethodAttributes.SpecialName,
					funcDef, umfpRef);

				var umfpValueRef = Module.ImportReference(umfpValue);

				umfpDlgt.GenerateIL(il => {
					var argNull = default(CecilLabel);
					if (EmitNullChecks) {
						argNull = il.DefineLabel();
					}
					il.Emit(OpCodes.Ldarg_0);

					if (EmitNullChecks) {
						il.Emit(OpCodes.Dup);
						il.Emit(OpCodes.Brfalse, argNull);
					}
					il.Emit(OpCodes.Ldfld, umfpValueRef);
					il.Emit(OpCodes.Call, getDelegateForFpMethodDef);
					il.Emit(OpCodes.Ret);

					if (EmitNullChecks) {
						il.MarkLabel(argNull);
						il.Emit(OpCodes.Newobj, ArgumentNullCtor);
						il.Emit(OpCodes.Throw);

						// ReSharper disable once PossibleNullReferenceException
						argNull.Cleanup();
					}
				});

				var dlgtUmfp = umfpDef.DefineMethod("op_Implicit",
					PublicStaticMethodAttributes | MethodAttributes.SpecialName,
					umfpRef, funcDef);

				var dlgtUmfpV0 = new VariableDefinition(umfpRef);
				dlgtUmfp.Body.Variables.Add(dlgtUmfpV0);
				dlgtUmfp.Body.InitLocals = true;

				dlgtUmfp.GenerateIL(il => {
					var argNull = default(CecilLabel);
					if (EmitNullChecks) {
						argNull = il.DefineLabel();
					}

					il.Emit(OpCodes.Ldloca_S, dlgtUmfpV0);
					il.Emit(OpCodes.Initobj, umfpRef);

					il.Emit(OpCodes.Ldloca_S, dlgtUmfpV0);
					il.Emit(OpCodes.Ldarg_0);

					if (EmitNullChecks) {
						il.Emit(OpCodes.Dup);
						il.Emit(OpCodes.Brfalse, argNull);
					}
					il.Emit(OpCodes.Call, getFpForDelegateMethodDef);
					il.Emit(OpCodes.Stfld, umfpValueRef);

					il.Emit(OpCodes.Ldloc_0);
					il.Emit(OpCodes.Ret);

					if (EmitNullChecks) {
						il.MarkLabel(argNull);
						il.Emit(OpCodes.Newobj, ArgumentNullCtor);
						il.Emit(OpCodes.Throw);

						// ReSharper disable once PossibleNullReferenceException
						argNull.Cleanup();
					}
				});

				var umfpPtr = umfpDef.DefineMethod("op_Implicit",
					PublicStaticMethodAttributes | MethodAttributes.SpecialName,
					Module.TypeSystem.IntPtr, umfpRef);

				umfpPtr.GenerateIL(il => {
					var argNull = default(CecilLabel);
					if (EmitNullChecks) {
						argNull = il.DefineLabel();
					}
					il.Emit(OpCodes.Ldarg_0);
					if (EmitNullChecks) {
						il.Emit(OpCodes.Dup);
						il.Emit(OpCodes.Brfalse, argNull);
					}
					il.Emit(OpCodes.Ldfld, umfpValueRef);
					il.Emit(OpCodes.Ret);

					if (EmitNullChecks) {
						il.MarkLabel(argNull);
						il.Emit(OpCodes.Newobj, ArgumentNullCtor);
						il.Emit(OpCodes.Throw);

						// ReSharper disable once PossibleNullReferenceException
						argNull.Cleanup();
					}
				});

				var ptrUmfp = umfpDef.DefineMethod("op_Implicit",
					PublicStaticMethodAttributes | MethodAttributes.SpecialName,
					umfpDef, Module.TypeSystem.IntPtr);

				var ptrUmfpV0 = new VariableDefinition(umfpRef);
				ptrUmfp.Body.Variables.Add(ptrUmfpV0);
				ptrUmfp.Body.InitLocals = true;

				ptrUmfp.GenerateIL(il => {
					var argNull = default(CecilLabel);
					if (EmitNullChecks) {
						argNull = il.DefineLabel();
					}
					il.Emit(OpCodes.Ldloca_S, ptrUmfpV0);
					il.Emit(OpCodes.Initobj, umfpRef);

					il.Emit(OpCodes.Ldloca_S, ptrUmfpV0);
					il.Emit(OpCodes.Ldarg_0);
					if (EmitNullChecks) {
						il.Emit(OpCodes.Dup);
						il.Emit(OpCodes.Brfalse, argNull);
					}
					il.Emit(OpCodes.Stfld, umfpValueRef);

					il.Emit(OpCodes.Ldloc_0);
					il.Emit(OpCodes.Ret);

					if (EmitNullChecks) {
						il.MarkLabel(argNull);
						il.Emit(OpCodes.Newobj, ArgumentNullCtor);
						il.Emit(OpCodes.Throw);

						// ReSharper disable once PossibleNullReferenceException
						argNull.Cleanup();
					}
				});

				var argTypes = argParams.Select(p => p.Type).ToArray();


				var retTypeDef = retType.Resolve();
				if (retTypeDef.BaseType != null && retTypeDef.BaseType.Is(MulticastDelegateType)) {
					// todo: marshalas umfp marshaller
					var ufpSpecTypeRef = Module.GetType(retTypeDef.FullName + "Unmanaged");
					Debug.Assert(ufpSpecTypeRef != null);
					retType = ufpSpecTypeRef;
				}

				for (var i = 0 ; i < argTypes.Length ; i++) {
					var argType = argTypes[i];
					var argTypeDef = argType.Resolve();
					if (argTypeDef.BaseType == null) continue;
					if (!argTypeDef.BaseType.Is(MulticastDelegateType))
						continue;

					// todo: marshalas umfp marshaller
					var ufpSpecTypeRef = Module.GetType(argTypeDef.FullName + "Unmanaged");
					Debug.Assert(ufpSpecTypeRef != null);
					argTypes[i] = ufpSpecTypeRef;
				}


				try {
					var ctor = funcDef.DefineConstructor(DelegateConstructorAttributes,
						Module.TypeSystem.Object, Module.TypeSystem.IntPtr);
					ctor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

					var method = funcDef.DefineMethod("Invoke",
						DelegateInvokeMethodAttributes,
						retType, argTypes);

					method.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

					// todo: figure out why the attribute is jacked up
					//method.SetCustomAttribute(callConvAttr);


					argParams.ConsumeLinkedList((argParam, i) => {
						var param = method.DefineParameter(i + 1, argParam.Attributes, argParam.Name);
					});

					return new[] {funcDef.CreateType()};
				}
				catch (Exception ex) {
					throw new InvalidProgramException("Critical function type definition failure.", ex);
				}
			};
		}

	}
}