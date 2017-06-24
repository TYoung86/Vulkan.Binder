using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleUInt64<T> : ITypedHandle<ulong> where T : IHandle<T> {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HandleUInt64(ulong value) => Value = value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HandleUInt64<T> other)
			=> Value == other.Value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleUInt64<T> handle
				&& Equals(handle);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> Value.GetHashCode();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(HandleUInt64<T> left, HandleUInt64<T> right)
			=> left.Equals(right);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(HandleUInt64<T> left, HandleUInt64<T> right)
			=> !left.Equals(right);

		public readonly ulong Value;

		object ITypedHandle.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		ulong ITypedHandle<ulong>.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ulong(HandleUInt64<T> handle)
			=> handle.Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HandleUInt64<T>(ulong value)
			=> new HandleUInt64<T>(value);
	}
}