using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Interop;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {

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
				MulticastDelegateType );
			funcDef.SetCustomAttribute(() => new BinderGeneratedAttribute());

			var umfpDef = new TypeDefinition("", "UnmanagedPointer",
				TypeAttributes.Sealed
				| TypeAttributes.SequentialLayout
				| TypeAttributes.NestedPublic,
				Module.TypeSystem.ValueType) {
				DeclaringType = funcDef
			};

			funcDef.NestedTypes.Add(umfpDef);

			umfpDef.DefineField("Value", Module.TypeSystem.IntPtr,
				FieldAttributes.Public
				| FieldAttributes.InitOnly);

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
			
				retParam.Complete(TypeRedirects,true);

				var retType = retParam.Type;

				foreach (var argParam in argParams)
					argParam.Complete(TypeRedirects,true);

				Debug.WriteLine($"Completed dependencies for function {funcName}");

				var argTypes = argParams.Select(p => p.Type).ToArray();
				
				
				var retTypeDef = retType.Resolve();
				if (retTypeDef.BaseType != null && retTypeDef.BaseType.Is(MulticastDelegateType)) {
					// todo: marshalas umfp marshaller
					var ufpSpecTypeRef = retTypeDef.NestedTypes.Single();
					retType = ufpSpecTypeRef;
				}

				for (var i = 0 ; i < argTypes.Length ; i++) {
					var argType = argTypes[i];
					var argTypeDef = argType.Resolve();
					if (argTypeDef.BaseType == null) continue;
					if (!argTypeDef.BaseType.Is(MulticastDelegateType))
						continue;
					// todo: marshalas umfp marshaller
					var ufpSpecTypeRef = retTypeDef.NestedTypes.Single();
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