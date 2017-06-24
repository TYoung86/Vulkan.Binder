using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleUIntPtr<T> : ITypedHandle<UIntPtr>, IEquatable<HandleUIntPtr<T>> where T : IHandle<T> {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HandleUIntPtr(UIntPtr value) => Value = value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HandleUIntPtr<T> other)
			=> Value.Equals(other.Value);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
			&& obj is HandleUIntPtr<T> handle
			&& Equals(handle);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> Value.GetHashCode();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(HandleUIntPtr<T> left, HandleUIntPtr<T> right)
			=> left.Equals(right);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(HandleUIntPtr<T> left, HandleUIntPtr<T> right)
			=> !left.Equals(right);

		public readonly UIntPtr Value;

		object ITypedHandle.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		UIntPtr ITypedHandle<UIntPtr>.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator UIntPtr(HandleUIntPtr<T> handle)
			=> handle.Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HandleUIntPtr<T>(UIntPtr value)
			=> new HandleUIntPtr<T>(value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe implicit operator HandleUIntPtr<T>(void* value)
			=> new HandleUIntPtr<T>((UIntPtr)value);
	}
}