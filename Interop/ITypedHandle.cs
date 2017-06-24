using System;

namespace Interop {
	public interface ITypedHandle {
		object Value { get; }
	}

	public interface ITypedHandle<out T> : ITypedHandle where T : struct {
		new T Value {
			get;
		}
	}
}