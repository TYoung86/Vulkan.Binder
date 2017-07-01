using System;
using System.Diagnostics;
using System.Linq;

namespace Vulkan.Binder {
	public static class StackGuard {
		public static bool LimitReentry(int i) {
			var offsetStackFrames = GetOffsetStackFrames();
			var caller = offsetStackFrames[0].GetMethod();
			var called = Enumerable.Count<StackFrame>(offsetStackFrames, sf => Object.Equals(sf.GetMethod(), caller));
			if (called <= i)
				return false;
			Debugger.Break();
			return true;
		}
		public static bool LimitRecursion(int i) {
			var offsetStackFrames = GetOffsetStackFrames();
			var caller = offsetStackFrames[0].GetMethod();
			var recursed = Enumerable.TakeWhile<StackFrame>(offsetStackFrames, sf => Object.Equals(sf.GetMethod(), caller)).Count();
			if (recursed <= i)
				return false;
			Debugger.Break();
			return true;
		}

		private static StackFrame[] GetOffsetStackFrames() {
			return Activator.CreateInstance<StackTrace>().GetFrames()
			.SkipWhile(sf => sf.GetMethod().DeclaringType != typeof(StackGuard))
			.SkipWhile(sf => sf.GetMethod().DeclaringType == typeof(StackGuard))
			.ToArray();
		}
	}
}