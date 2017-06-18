using System;
using System.Collections.Generic;
using System.Linq;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<TypeDefinition[]> DefineClrType(ClangFunctionInfoBase funcInfo32, ClangFunctionInfoBase funcInfo64) {
			if (funcInfo32 == funcInfo64) {
				return DefineClrType(funcInfo64);
			}

			throw new NotImplementedException();
		}

		private Func<TypeDefinition[]> DefineClrType(ClangFunctionInfoBase funcInfo) {
			var funcDef = Module.DefineType(funcInfo.Name,
				DelegateTypeAttributes,
				typeof(MulticastDelegate) );

			var retParam = ResolveParameter(funcInfo.ReturnType);
			if (!CallingConventionMap.TryGetValue(funcInfo.CallConvention, out var callConv))
				throw new NotImplementedException();
			if (!ClrCallingConventionAttributeMap.TryGetValue(callConv, out var callConvAttr))
				throw new NotImplementedException();
			var argParams = new LinkedList<ParameterInfo>(funcInfo.Parameters.Select(p => ResolveParameter(p.Type, p.Name, (int) p.Index)));

			return () => {
				retParam.RequireCompleteTypeReferences(true);

				var retType = retParam.Type;

				foreach (var argParam in argParams)
					argParam.RequireCompleteTypeReferences(true);

				var clrArgTypes = argParams.Select(p => p.Type).ToArray();

				try {
					var ctor = funcDef.DefineConstructor(DelegateConstructorAttributes,
						typeof(object), typeof(IntPtr));
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