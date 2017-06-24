using System;
using System.Runtime.CompilerServices;

namespace Interop
{
    public unsafe struct SplitPointer<TI,T32,T64>
		where TI : class
		where T32 : struct, TI
		where T64 : struct, TI {
		
	    public SplitPointer(void * ptr) => _p = ptr;
	    public SplitPointer(IntPtr ptr) => _p = (void*) ptr;
	    public SplitPointer(UIntPtr ptr) => _p = (void*) ptr;

	    private readonly void* _p;

	    private ref TI Target {
		    get {
			    if ( IntPtr.Size == 8 )
				    return ref Unsafe.As<T64, TI>(ref GetTarget64());
				return ref Unsafe.As<T32, TI>(ref GetTarget32());
		    }
	    }

		
	    private ref T32 GetTarget32() => ref Unsafe.AsRef<T32>(_p);
	    private ref T64 GetTarget64() => ref Unsafe.AsRef<T64>(_p);
    }
}
