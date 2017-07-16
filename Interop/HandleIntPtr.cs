using System;

namespace Interop {
	public struct HandleIntPtr<T> : ITypedHandle<IntPtr> where T : IHandle<T> {

		public HandleIntPtr(IntPtr value) => Value = value;

		public bool Equals(HandleIntPtr<T> other)
			=> Value.Equals(other.Value);

		public override bool Equals(object obj)
			=> !ReferenceEquals(null, obj)
				&& obj is HandleIntPtr<T> handle
				&& Equals(handle);

		public override int GetHashCode()
			=> Value.GetHashCode();

		public static bool operator ==(HandleIntPtr<T> left, HandleIntPtr<T> right)
			=> left.Equals(right);

		public static bool operator !=(HandleIntPtr<T> left, HandleIntPtr<T> right)
			=> !left.Equals(right);

		public readonly IntPtr Value;

		object ITypedHandle.Value {

			get => Value;
		}

		IntPtr ITypedHandle<IntPtr>.Value {

			get => Value;
		}

		public static implicit operator IntPtr(HandleIntPtr<T> handle)
			=> handle.Value;

		public static implicit operator HandleIntPtr<T>(IntPtr value)
			=> new HandleIntPtr<T>(value);

		public static unsafe implicit operator HandleIntPtr<T>(void* value)
			=> new HandleIntPtr<T>((IntPtr)value);
	}
}