using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleInt64<T> : ITypedHandle<long> where T : IHandle<T> {

		public HandleInt64(long value) => Value = value;

		public bool Equals(HandleInt64<T> other)
			=> Value == other.Value;

		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleInt64<T> handle
				&& Equals(handle);

		public override int GetHashCode()
			=> Value.GetHashCode();

		public static bool operator ==(HandleInt64<T> left, HandleInt64<T> right)
			=> left.Equals(right);

		public static bool operator !=(HandleInt64<T> left, HandleInt64<T> right)
			=> !left.Equals(right);

		public readonly long Value;

		object ITypedHandle.Value {

			get => Value;
		}

		long ITypedHandle<long>.Value {

			get => Value;
		}

		public static implicit operator long(HandleInt64<T> handle)
			=> handle.Value;

		public static implicit operator HandleInt64<T>(long value)
			=> new HandleInt64<T>(value);
	}
}