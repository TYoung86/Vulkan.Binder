using System;
using Mono.Cecil.Cil;

namespace Vulkan.Binder.Extensions {
	public static class CecilLabelExtensions {
		public static void Emit(this ILProcessor ilp, OpCode opCode, CecilLabel label) {
			if (!label.IsSameILProcessor(ilp))
				throw new NotSupportedException();
			label.Emit(opCode);
		}

		public static CecilLabel DefineLabel(this ILProcessor ilp) {
			return new CecilLabel(ilp);
		}

		public static void MarkLabel(this ILProcessor ilp, CecilLabel label) {
			if (!label.IsSameILProcessor(ilp))
				throw new NotSupportedException();
			label.Mark();
		}
	}
}