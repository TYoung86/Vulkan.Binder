using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleIntPtr<T> : ITypedHandle<IntPtr> where T : IHandle<T> {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HandleIntPtr(IntPtr value) => Value = value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HandleIntPtr<T> other)
			=> Value.Equals(other.Value);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleIntPtr<T> handle
				&& Equals(handle);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> Value.GetHashCode();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(HandleIntPtr<T> left, HandleIntPtr<T> right)
			=> left.Equals(right);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(HandleIntPtr<T> left, HandleIntPtr<T> right)
			=> !left.Equals(right);

		public readonly IntPtr Value;

		object ITypedHandle.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		IntPtr ITypedHandle<IntPtr>.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator IntPtr(HandleIntPtr<T> handle)
			=> handle.Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HandleIntPtr<T>(IntPtr value)
			=> new HandleIntPtr<T>(value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe implicit operator HandleIntPtr<T>(void* value)
			=> new HandleIntPtr<T>((IntPtr)value);
	}
}