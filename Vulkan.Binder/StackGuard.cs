using System;
using System.Diagnostics;
using System.Linq;

namespace Vulkan.Binder {
	public static class StackGuard {

		public static bool LimitEntry(int i) {
			var offsetStackFrames = GetOffsetStackFrames();
			var caller = offsetStackFrames[0].GetMethod();
			var called = offsetStackFrames.Count(sf => Equals(sf.GetMethod(), caller));
			return called > i;
		}
		public static bool LimitRecursion(int i) {
			var offsetStackFrames = GetOffsetStackFrames();
			var caller = offsetStackFrames[0].GetMethod();
			var recursed = offsetStackFrames.TakeWhile(sf => Equals(sf.GetMethod(), caller)).Count();
			return recursed > i;
		}

		private static StackFrame[] GetOffsetStackFrames() {
			return Activator.CreateInstance<StackTrace>().GetFrames()
			.SkipWhile(sf => sf.GetMethod().DeclaringType != typeof(StackGuard))
			.SkipWhile(sf => sf.GetMethod().DeclaringType == typeof(StackGuard))
			.ToArray();
		}

		[Conditional("DEBUG")]
		public static void DebugLimitEntry(int i) {
			if ( LimitEntry(i) ) Debugger.Break();
		}

		[Conditional("DEBUG")]
		// ReSharper disable once RedundantAssignment // can't use out on conditional method
		public static void DebugLimitEntry(int i, ref bool hit) {
			hit = LimitEntry(i);
		}

		[Conditional("DEBUG")]
		public static void DebugLimitRecursion(int i) {
			if ( LimitRecursion(i) ) Debugger.Break();
		}

		[Conditional("DEBUG")]
		// ReSharper disable once RedundantAssignment // can't use out on conditional method
		public static void DebugLimitRecursion(int i, ref bool hit) {
			hit = LimitRecursion(i);
		}
	}
}