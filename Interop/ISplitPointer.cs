namespace Interop {
	public interface ISplitPointer<TI, T32, T64> : IPointer<TI>
		where TI : class
		where T32 : struct, TI
		where T64 : struct, TI {

		ref T32 GetTarget32();

		ref T64 GetTarget64();

	}
}