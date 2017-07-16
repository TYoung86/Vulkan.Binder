using System;
using System.Runtime.CompilerServices;

namespace Interop {
	public unsafe struct SplitPointer<TI, T32, T64> : ISplitPointer<TI, T32, T64>
		where TI : class
		where T32 : struct, TI
		where T64 : struct, TI {

		public SplitPointer(void* ptr) => Pointer = ptr;

		public SplitPointer(IntPtr ptr) => Pointer = (void*) ptr;

		public SplitPointer(UIntPtr ptr) => Pointer = (void*) ptr;

		public readonly void* Pointer;

		public ref TI Target {
			get {
				if (IntPtr.Size == 8)
					return ref Unsafe.As<T64, TI>(ref GetTarget64());

				return ref Unsafe.As<T32, TI>(ref GetTarget32());
			}
		}

		public ref T32 GetTarget32() => ref Unsafe.AsRef<T32>(Pointer);

		public ref T64 GetTarget64() => ref Unsafe.AsRef<T64>(Pointer);

	}
}