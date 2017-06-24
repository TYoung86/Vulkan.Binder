using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleUInt32<T> : ITypedHandle<uint> where T : IHandle<T> {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HandleUInt32(uint value) => Value = value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HandleUInt32<T> other)
			=> Value == other.Value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleUInt32<T> handle
				&& Equals(handle);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> Value.GetHashCode();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(HandleUInt32<T> left, HandleUInt32<T> right)
			=> left.Equals(right);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(HandleUInt32<T> left, HandleUInt32<T> right)
			=> !left.Equals(right);

		public readonly uint Value;

		object ITypedHandle.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		uint ITypedHandle<uint>.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator uint(HandleUInt32<T> handle)
			=> handle.Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HandleUInt32<T>(uint value)
			=> new HandleUInt32<T>(value);
	}
}