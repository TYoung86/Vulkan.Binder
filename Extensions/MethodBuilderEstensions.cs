using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;

namespace Artilect.Vulkan.Binder.Extensions {
	public static class MethodBuilderEstensions {

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		public static void GenerateIL(this MethodBuilder mb, Action<ILGenerator> generator)
			=> generator(mb.GetILGenerator());
		
		public static void EmitPushConst(this ILGenerator ilg, int value) {
			switch (value) {
				case -1: ilg.Emit(OpCodes.Ldc_I4_M1); return;
				case 0: ilg.Emit(OpCodes.Ldc_I4_0); return;
				case 1: ilg.Emit(OpCodes.Ldc_I4_1); return;
				case 2: ilg.Emit(OpCodes.Ldc_I4_2); return;
				case 3: ilg.Emit(OpCodes.Ldc_I4_3); return;
				case 4: ilg.Emit(OpCodes.Ldc_I4_4); return;
				case 5: ilg.Emit(OpCodes.Ldc_I4_5); return;
				case 6: ilg.Emit(OpCodes.Ldc_I4_6); return;
				case 7: ilg.Emit(OpCodes.Ldc_I4_7); return;
				case 8: ilg.Emit(OpCodes.Ldc_I4_8); return;
			}
			if (value >= -128 && value <= 127) {
				ilg.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
				return;
			}
			ilg.Emit(OpCodes.Ldc_I4, value);
		}
		public static void EmitPushConst(this ILGenerator ilg, long value) {
			ilg.Emit(OpCodes.Ldc_I8, value);
		}
		public static void EmitPushConst(this ILGenerator ilg, float value) {
			ilg.Emit(OpCodes.Ldc_R4, value);
		}
		public static void EmitPushConst(this ILGenerator ilg, double value) {
			ilg.Emit(OpCodes.Ldc_R8, value);
		}
	}
}