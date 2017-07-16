namespace Interop {
	public struct HandleUInt32<T> : ITypedHandle<uint> where T : IHandle<T> {

		public HandleUInt32(uint value) => Value = value;

		public bool Equals(HandleUInt32<T> other)
			=> Value == other.Value;

		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleUInt32<T> handle
				&& Equals(handle);

		public override int GetHashCode()
			=> Value.GetHashCode();

		public static bool operator ==(HandleUInt32<T> left, HandleUInt32<T> right)
			=> left.Equals(right);

		public static bool operator !=(HandleUInt32<T> left, HandleUInt32<T> right)
			=> !left.Equals(right);

		public readonly uint Value;

		object ITypedHandle.Value {

			get => Value;
		}

		uint ITypedHandle<uint>.Value {

			get => Value;
		}

		public static implicit operator uint(HandleUInt32<T> handle)
			=> handle.Value;

		public static implicit operator HandleUInt32<T>(uint value)
			=> new HandleUInt32<T>(value);
	}
}