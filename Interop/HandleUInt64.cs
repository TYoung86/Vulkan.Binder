using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleUInt64<T> : ITypedHandle<ulong> where T : IHandle<T> {

		public HandleUInt64(ulong value) => Value = value;

		public bool Equals(HandleUInt64<T> other)
			=> Value == other.Value;

		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleUInt64<T> handle
				&& Equals(handle);

		public override int GetHashCode()
			=> Value.GetHashCode();

		public static bool operator ==(HandleUInt64<T> left, HandleUInt64<T> right)
			=> left.Equals(right);

		public static bool operator !=(HandleUInt64<T> left, HandleUInt64<T> right)
			=> !left.Equals(right);

		public readonly ulong Value;

		object ITypedHandle.Value {

			get => Value;
		}

		ulong ITypedHandle<ulong>.Value {

			get => Value;
		}

		public static implicit operator ulong(HandleUInt64<T> handle)
			=> handle.Value;

		public static implicit operator HandleUInt64<T>(ulong value)
			=> new HandleUInt64<T>(value);
	}
}