using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleInt32<T> : ITypedHandle<int> where T : IHandle<T> {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HandleInt32(int value) => Value = value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HandleInt32<T> other)
			=> Value == other.Value;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleInt32<T> handle
				&& Equals(handle);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> Value.GetHashCode();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(HandleInt32<T> left, HandleInt32<T> right)
			=> left.Equals(right);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(HandleInt32<T> left, HandleInt32<T> right)
			=> !left.Equals(right);

		public readonly int Value;

		object ITypedHandle.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		int ITypedHandle<int>.Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator int(HandleInt32<T> handle)
			=> handle.Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator HandleInt32<T>(int value)
			=> new HandleInt32<T>(value);
	}
}