using System;
using System.Linq;
using Artilect.Interop;
using Artilect.Vulkan.Binder.Extensions;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private static bool IsIntPtrOrUIntPtr(Type t) {
			return t == typeof(IntPtr) || t == typeof(UIntPtr);
		}

		private static bool IsHandleType(Type t) {
			Type interfaceType;

			if (t.IsPrimitive || t.IsIndirect()) return false;

			try {
				interfaceType = typeof(IHandle<>).MakeGenericType(t);
			}
			catch {
				return false;
			}

			var result = t.GetInterfaces().Contains(interfaceType);

			return result;
		}

		private static bool IsTypedHandle(Type t) {
			try {
				return typeof(ITypedHandle).IsAssignableFrom(t);
			}
			catch {
				return false;
			}
		}

		private static bool IsTypedHandle(Type t, Type e) {
			Type interfaceType;

			if (e.IsPointer) {
				return IsTypedHandle(t)
					&& t.GenericTypeArguments[0] == e.GetElementType();
			}

			if (!e.IsPrimitive || e.IsIndirect()
				|| t.IsPrimitive || t.IsIndirect()) return false;

			try {
				interfaceType = typeof(ITypedHandle<>).MakeGenericType(e);
			}
			catch {
				return false;
			}

			var result = t.GetInterfaces().Contains(interfaceType);

			return result;
		}
	}
}