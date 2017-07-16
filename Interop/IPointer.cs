namespace Interop {
	public interface IPointer<T> {
		ref T Target { get; }
	}
}