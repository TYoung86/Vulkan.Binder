using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Interop {
	[SuppressMessage("ReSharper", "UnusedTypeParameter")]
	public struct HandleUIntPtr<T> : ITypedHandle<UIntPtr>, IEquatable<HandleUIntPtr<T>> where T : IHandle<T> {

		public HandleUIntPtr(UIntPtr value) => Value = value;

		public bool Equals(HandleUIntPtr<T> other)
			=> Value.Equals(other.Value);

		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
			&& obj is HandleUIntPtr<T> handle
			&& Equals(handle);

		public override int GetHashCode()
			=> Value.GetHashCode();

		public static bool operator ==(HandleUIntPtr<T> left, HandleUIntPtr<T> right)
			=> left.Equals(right);

		public static bool operator !=(HandleUIntPtr<T> left, HandleUIntPtr<T> right)
			=> !left.Equals(right);

		public readonly UIntPtr Value;

		object ITypedHandle.Value {

			get => Value;
		}

		UIntPtr ITypedHandle<UIntPtr>.Value {

			get => Value;
		}

		public static implicit operator UIntPtr(HandleUIntPtr<T> handle)
			=> handle.Value;

		public static implicit operator HandleUIntPtr<T>(UIntPtr value)
			=> new HandleUIntPtr<T>(value);

		public static unsafe implicit operator HandleUIntPtr<T>(void* value)
			=> new HandleUIntPtr<T>((UIntPtr)value);
	}
}