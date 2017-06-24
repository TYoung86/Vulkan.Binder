using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleInt64<T> : ITypedHandle<long> where T : IHandle<T> {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HandleInt64(long value) => Value = value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HandleInt64<T> other)
			=> Value == other.Value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleInt64<T> handle
				&& Equals(handle);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> Value.GetHashCode();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(HandleInt64<T> left, HandleInt64<T> right)
			=> left.Equals(right);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(HandleInt64<T> left, HandleInt64<T> right)
			=> !left.Equals(right);

		public readonly long Value;

		object ITypedHandle.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		long ITypedHandle<long>.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator long(HandleInt64<T> handle)
			=> handle.Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HandleInt64<T>(long value)
			=> new HandleInt64<T>(value);
	}
}