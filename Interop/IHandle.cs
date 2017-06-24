namespace Interop {

	public interface IHandle<T>
		where T : IHandle<T> {}
}