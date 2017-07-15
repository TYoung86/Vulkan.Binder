using System;
using System.Collections.Concurrent;

namespace Interop {
	internal static class Utf8StringInternal {
		
#if NETSTANDARD2_0
		internal static readonly ConcurrentDictionary<string, Utf8String> Interned
			= new ConcurrentDictionary<string, Utf8String>();
#endif

		internal static readonly ConcurrentDictionary<IntPtr, uint> Allocated
			= new ConcurrentDictionary<IntPtr, uint>();

		internal static readonly ConcurrentDictionary<IntPtr, uint> ByteLengthCache
			= new ConcurrentDictionary<IntPtr, uint>();

		internal static readonly ConcurrentDictionary<IntPtr, uint> CharCountCache
			= new ConcurrentDictionary<IntPtr, uint>();

		internal static readonly ConcurrentDictionary<IntPtr, string> StringCache
			= new ConcurrentDictionary<IntPtr, string>();

		internal static readonly ConcurrentDictionary<IntPtr, int> HashCodeCache
			= new ConcurrentDictionary<IntPtr, int>();

	}
}