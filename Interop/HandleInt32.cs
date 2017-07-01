using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleInt32<T> : ITypedHandle<int> where T : IHandle<T> {
		public HandleInt32(int value) => Value = value;
		
		public bool Equals(HandleInt32<T> other)
			=> Value == other.Value;
		
		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleInt32<T> handle
				&& Equals(handle);

		public override int GetHashCode()
			=> Value.GetHashCode();

		public static bool operator ==(HandleInt32<T> left, HandleInt32<T> right)
			=> left.Equals(right);

		public static bool operator !=(HandleInt32<T> left, HandleInt32<T> right)
			=> !left.Equals(right);

		public readonly int Value;

		object ITypedHandle.Value {

			get => Value;
		}

		int ITypedHandle<int>.Value {

			get => Value;
		}

		public static implicit operator int(HandleInt32<T> handle)
			=> handle.Value;

		public static implicit operator HandleInt32<T>(int value)
			=> new HandleInt32<T>(value);
	}
}