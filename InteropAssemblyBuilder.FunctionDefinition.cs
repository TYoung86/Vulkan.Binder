using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Artilect.Vulkan.Binder.Extensions;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<Type[]> DefineClrType(ClangFunctionInfoBase funcInfo32, ClangFunctionInfoBase funcInfo64) {
			if (funcInfo32 == funcInfo64) {
				return DefineClrType(funcInfo64);
			}

			throw new NotImplementedException();
		}

		private Func<Type[]> DefineClrType(ClangFunctionInfoBase funcInfo) {
			TypeBuilder funcDef = Module.DefineType(funcInfo.Name,
				DelegateTypeAttributes,
				typeof(MulticastDelegate) );

			var retParam = ResolveParameter(funcInfo.ReturnType);
			if (!CallingConventionMap.TryGetValue(funcInfo.CallConvention, out var callConv))
				throw new NotImplementedException();
			if (!ClrCallingConventionAttributeMap.TryGetValue(callConv, out var callConvAttr))
				throw new NotImplementedException();
			var argParams = new LinkedList<CustomParameterInfo>(funcInfo.Parameters.Select(p => ResolveParameter(p.Type, p.Name, (int) p.Index)));

			return () => {
				retParam.RequireCompleteTypes(true);

				var retType = retParam.ParameterType;

				foreach (var argParam in argParams)
					argParam.RequireCompleteTypes(true);

				var clrArgTypes = argParams.Select(p => p.ParameterType).ToArray();

				try {
					var ctor = funcDef.DefineConstructor(DelegateConstructorAttributes,
						CallingConventions.Standard, new[] {typeof(object), typeof(IntPtr)});
					ctor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

					if (retParam.CustomAttributes.Any())
						throw new NotImplementedException();

					var method = funcDef.DefineMethod("Invoke",
						DelegateInvokeMethodAttributes,
						retType, clrArgTypes);

					method.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);
					method.SetCustomAttribute(callConvAttr);


					argParams.ConsumeLinkedList((argParam, i) => {
						var param = method.DefineParameter(i + 1, argParam.Attributes, argParam.Name);
						foreach (var attrInfo in argParam.AttributeInfos)
							param.SetCustomAttribute(attrInfo.GetCustomAttributeBuilder());
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