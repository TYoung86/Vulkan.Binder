using System;
using System.Linq;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {

		private bool IsIntPtrOrUIntPtr(TypeReference t) {
			return t.Is(IntPtrType) || t.Is(UIntPtrType);
		}

		private bool IsHandleType(TypeReference t) {
			TypeReference interfaceType;

			if (t.IsPrimitive || t.IsIndirect()) return false;

			try {
				interfaceType = IHandleGtd.MakeGenericInstanceType(t);
			}
			catch {
				return false;
			}

			var result = t.GetInterfaces().Contains(interfaceType); //.Any( i => i.Is(interfaceType));

			return result;
		}

		private bool IsTypedHandle(TypeReference t) {
			try {
				return ITypedHandleType.IsAssignableFrom(t);
			}
			catch {
				return false;
			}
		}

		private bool IsTypedHandle(TypeReference t, TypeReference e) {
			TypeReference interfaceType;

			if (e.IsPointer) {
				if (IsTypedHandle(t)) {
					var gt = (GenericInstanceType) t;
					return gt.GenericArguments[0] == e.DescendElementType();
				}
			}

			if (!e.IsPrimitive || e.IsIndirect()
				|| t.IsPrimitive || t.IsIndirect()) return false;

			try {
				interfaceType = ITypedHandleGtd.MakeGenericInstanceType(e);
			}
			catch {
				return false;
			}
			
			var result = t.GetInterfaces().Contains(interfaceType); //.Any( i => i.Is(interfaceType));

			return result;
		}
	}
}