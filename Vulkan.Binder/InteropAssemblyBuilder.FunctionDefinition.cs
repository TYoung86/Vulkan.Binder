using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Interop;
using Mono.Cecil;
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

			var retParam = ResolveParameter(funcInfo.ReturnType);
			if (!CallingConventionMap.TryGetValue(funcInfo.CallConvention, out var callConv))
				throw new NotImplementedException();
			if (!ClrCallingConventionAttributeMap.TryGetValue(callConv, out var callConvAttr))
				throw new NotImplementedException();
			var argParams = new LinkedList<ParameterInfo>(funcInfo.Parameters.Select(p => ResolveParameter(p.Type, p.Name, (int) p.Index)));

			return () => {
			
				retParam.Complete(TypeRedirects,true);

				var retType = retParam.Type;

				foreach (var argParam in argParams)
					argParam.Complete(TypeRedirects,true);

				Debug.WriteLine($"Completed dependencies for function {funcName}");

				var clrArgTypes = argParams.Select(p => p.Type).ToArray();

				try {
					var ctor = funcDef.DefineConstructor(DelegateConstructorAttributes,
						Module.TypeSystem.Object, Module.TypeSystem.IntPtr);
					ctor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

					var method = funcDef.DefineMethod("Invoke",
						DelegateInvokeMethodAttributes,
						retType, clrArgTypes);

					method.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);
					method.SetCustomAttribute(callConvAttr);


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