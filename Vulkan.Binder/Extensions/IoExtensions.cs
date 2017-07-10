using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Vulkan.Binder.Extensions {
	public static class IoExtensions {
		public unsafe delegate long BytePointerWriter(byte*p);

		public unsafe delegate void BytePointerAccessor(byte*p);

		// buffers > 85000 to avoid going to small object heap
		private const int BufferBlockSize = 163840; //160kB, 40 pages

		private const int MaxThreadBlockSize = 16777216; // 16mB, 4096 pages


		public static unsafe void UsePointer(this MemoryMappedViewAccessor mmv, BytePointerAccessor f) {
			var h = mmv.SafeMemoryMappedViewHandle;
			byte* p = null;
			h.AcquirePointer(ref p);
			f(p);
			h.ReleasePointer();
		}

		public static unsafe void UsePointer(this UnmanagedMemoryStream ums, BytePointerWriter f) {
			if (ums is MemoryMappedViewStream mmvs) {
				byte* p = null;
				var h = mmvs.SafeMemoryMappedViewHandle;
				h.AcquirePointer(ref p);
				var diff = f(p + ums.Position);
				h.ReleasePointer();
				ums.Position += diff;
				return;
			}
			ums.Position += f(ums.PositionPointer);
		}

		public static unsafe void UsePointer(this MemoryStream ms, BytePointerWriter f) {
			void ExecAndTrack(byte* p)
				=> ms.Position += f(p);

			if (ms.TryGetBuffer(out var buf))
				fixed (byte* p = &buf.Array[buf.Offset])
					ExecAndTrack(p);

			fixed (byte* p = &ms.ToArray()[ms.Position])
				ExecAndTrack(p);
		}


		public static unsafe void Consume(this MemoryMappedViewAccessor dest, Stream s) {
			var hdest = dest.SafeMemoryMappedViewHandle;
			// catch any overflow on conversion operations
			checked {
				var limit = (long)hdest.ByteLength - dest.PointerOffset;
				dest.UsePointer(pdest => {
					if (s is UnmanagedMemoryStream ums) {
						var writable = Math.Min(limit, ums.Length);
						var blockSize = (int)Math.Min(writable, MaxThreadBlockSize);
						if (writable > blockSize) {
							// multithreaded block copy
							ums.UsePointer(pums => {
								var taskCount = (writable + (blockSize - 1)) / blockSize;
								var tasks = new Task[taskCount];
								for (var i = 0 ; i < taskCount ; ++i) {
									var offset = i * blockSize;
									var taskDestP = pdest + offset;
									var taskUmsP = pums + offset;
									var taskSize = (uint)Math.Min(blockSize, writable - offset);
									tasks[i] = Task.Run(() => {
										Unsafe.CopyBlock(taskDestP, taskUmsP, taskSize);
									});
								}
								Task.WaitAll(tasks);
								return writable;
							});

						}
						// synchronous
						ums.UsePointer(pums => {
							Unsafe.CopyBlock(pdest, pums,
								(uint)writable);
							return writable;
						});
					}

					if (s is MemoryStream ms) {
						var writable = Math.Min(limit, ms.Length);
						var blockSize = (int)Math.Min(writable, MaxThreadBlockSize);
						if (writable > blockSize) {
							// multithreaded block copy
							ms.UsePointer(pums => {
								var taskCount = (writable + (blockSize - 1)) / blockSize;
								var tasks = new Task[taskCount];
								for (var i = 0 ; i < taskCount ; ++i) {
									var offset = i * blockSize;
									var taskDestP = pdest + offset;
									var taskUmsP = pums + offset;
									var taskSize = (uint)Math.Min(blockSize, writable - offset);
									tasks[i] = Task.Run(() => {
										Unsafe.CopyBlock(taskDestP, taskUmsP, taskSize);
									});
								}
								Task.WaitAll(tasks);
								return writable;
							});

						}
						// synchronous
						ms.UsePointer(pums => {
							Unsafe.CopyBlock(pdest, pums,
								(uint)writable);
							return writable;
						});
					}

					var streamLength = long.MaxValue;
					try {
						streamLength = s.Length;
					} catch { /* oh well, blind copy */ }

					if (limit > MaxThreadBlockSize) {
						// dual-threaded overlapped block copy
						var blockSize = (int) Math.Min(MaxThreadBlockSize,
							Math.Min(limit, streamLength));
						var buf1 = ArrayPool<byte>.Shared
							.Rent(blockSize);
						var buf2 = ArrayPool<byte>.Shared
							.Rent(blockSize);
						long written = 0;
						var readTask1 = Task.FromResult(s.Read(buf1, 0, blockSize));
						if (readTask1.Result <= 0) return;
						for (; ;) {
							var readTask2 = s.ReadAsync(buf2, 0, blockSize);
							var readBytes1 = readTask1.Result;
							fixed (byte* psrc = &buf1[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) readBytes1);
							if (readBytes1 == 0 && readTask2.Result == 0) break;
							written += readBytes1;

							readTask1 = s.ReadAsync(buf1, 0, blockSize);
							var readBytes2 = readTask2.Result;
							fixed (byte* psrc = &buf2[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) readBytes2);
							if (readBytes2 == 0 && readTask1.Result == 0) break;
							written += readBytes2;
						}
						ArrayPool<byte>.Shared.Return(buf1);
						ArrayPool<byte>.Shared.Return(buf2);
						return;
					}
					else {
						// synchronous
						var blockSize = (int) Math.Min(limit, streamLength);
						var buf = ArrayPool<byte>.Shared
							.Rent(blockSize);
						long written = 0;
						var read = s.Read(buf, 0, blockSize);
						if (read <= 0) return;
						do {
							fixed (byte* psrc = &buf[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) read);
							written += read;
							read = s.Read(buf, 0, blockSize);
						} while (read > 0);
						ArrayPool<byte>.Shared.Return(buf);
						return;
					}
				});
			}
		}

		public static unsafe void Consume(this MemoryStream dest, Stream s) {
			// catch any overflow on conversion operations
			checked {
				var limit = dest.Length - dest.Position;
				dest.UsePointer(pdest => {
					if (s is UnmanagedMemoryStream ums) {
						var writable = Math.Min(limit, ums.Length);
						var blockSize = (int)Math.Min(writable, MaxThreadBlockSize);
						if (writable > blockSize) {
							// multithreaded block copy
							ums.UsePointer(pums => {
								var taskCount = (writable + (blockSize - 1)) / blockSize;
								var tasks = new Task[taskCount];
								for (var i = 0 ; i < taskCount ; ++i) {
									var offset = i * blockSize;
									var taskDestP = pdest + offset;
									var taskUmsP = pums + offset;
									var taskSize = (uint)Math.Min(blockSize, writable - offset);
									tasks[i] = Task.Run(() => {
										Unsafe.CopyBlock(taskDestP, taskUmsP, taskSize);
									});
								}
								Task.WaitAll(tasks);
								return writable;
							});
							return writable;

						}
						// synchronous
						ums.UsePointer(pums => {
							Unsafe.CopyBlock(pdest, pums,
								(uint)writable);
							return writable;
						});
						return writable;
					}

					if (s is MemoryStream ms) {
						var writable = Math.Min(limit, ms.Length);
						var blockSize = (int)Math.Min(writable, MaxThreadBlockSize);
						if (writable > blockSize) {
							// multithreaded block copy
							ms.UsePointer(pums => {
								var taskCount = (writable + (blockSize - 1)) / blockSize;
								var tasks = new Task[taskCount];
								for (var i = 0 ; i < taskCount ; ++i) {
									var offset = i * blockSize;
									var taskDestP = pdest + offset;
									var taskUmsP = pums + offset;
									var taskSize = (uint)Math.Min(blockSize, writable - offset);
									tasks[i] = Task.Run(() => {
										Unsafe.CopyBlock(taskDestP, taskUmsP, taskSize);
									});
								}
								Task.WaitAll(tasks);
								return writable;
							});
							return writable;

						}
						// synchronous
						ms.UsePointer(pums => {
							Unsafe.CopyBlock(pdest, pums,
								(uint)writable);
							return writable;
						});
						return writable;
					}

					var streamLength = long.MaxValue;
					try {
						streamLength = s.Length;
					} catch { /* oh well, blind copy */ }

					if (limit > MaxThreadBlockSize) {
						// dual-threaded overlapped block copy
						var blockSize = (int) Math.Min(MaxThreadBlockSize,
							Math.Min(limit, streamLength));
						var buf1 = ArrayPool<byte>.Shared
							.Rent(blockSize);
						var buf2 = ArrayPool<byte>.Shared
							.Rent(blockSize);
						long written = 0;
						var readTask1 = Task.FromResult(s.Read(buf1, 0, blockSize));
						if (readTask1.Result <= 0) return 0;
						for (; ;) {
							var readTask2 = s.ReadAsync(buf2, 0, blockSize);
							var readBytes1 = readTask1.Result;
							fixed (byte* psrc = &buf1[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) readBytes1);
							if (readBytes1 == 0 && readTask2.Result == 0) break;
							written += readBytes1;

							readTask1 = s.ReadAsync(buf1, 0, blockSize);
							var readBytes2 = readTask2.Result;
							fixed (byte* psrc = &buf2[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) readBytes2);
							if (readBytes2 == 0 && readTask1.Result == 0) break;
							written += readBytes2;
						}
						ArrayPool<byte>.Shared.Return(buf1);
						ArrayPool<byte>.Shared.Return(buf2);
						return written;
					}
					else {
						// synchronous
						var blockSize = (int) Math.Min(limit, streamLength);
						var buf = ArrayPool<byte>.Shared
							.Rent(blockSize);
						long written = 0;
						var read = s.Read(buf, 0, blockSize);
						if (read <= 0) return 0;
						do {
							fixed (byte* psrc = &buf[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) read);
							written += read;
							read = s.Read(buf, 0, blockSize);
						} while (read > 0);
						ArrayPool<byte>.Shared.Return(buf);
						return written;
					}
				});
			}
		}

		public static unsafe void Consume(this UnmanagedMemoryStream dest, Stream s) {
			// catch any overflow on conversion operations
			checked {
				var limit = dest.Length - dest.Position;
				dest.UsePointer(pdest => {
					if (s is UnmanagedMemoryStream ums) {
						var writable = Math.Min(limit, ums.Length);
						var blockSize = (int)Math.Min(writable, MaxThreadBlockSize);
						if (writable > blockSize) {
							// multithreaded block copy
							ums.UsePointer(pums => {
								var taskCount = (writable + (blockSize - 1)) / blockSize;
								var tasks = new Task[taskCount];
								for (var i = 0 ; i < taskCount ; ++i) {
									var offset = i * blockSize;
									var taskDestP = pdest + offset;
									var taskUmsP = pums + offset;
									var taskSize = (uint)Math.Min(blockSize, writable - offset);
									tasks[i] = Task.Run(() => {
										Unsafe.CopyBlock(taskDestP, taskUmsP, taskSize);
									});
								}
								Task.WaitAll(tasks);
								return writable;
							});
							return writable;

						}
						// synchronous
						ums.UsePointer(pums => {
							Unsafe.CopyBlock(pdest, pums,
								(uint)writable);
							return writable;
						});
						return writable;
					}

					if (s is MemoryStream ms) {
						var writable = Math.Min(limit, ms.Length);
						var blockSize = (int)Math.Min(writable, MaxThreadBlockSize);
						if (writable > blockSize) {
							// multithreaded block copy
							ms.UsePointer(pums => {
								var taskCount = (writable + (blockSize - 1)) / blockSize;
								var tasks = new Task[taskCount];
								for (var i = 0 ; i < taskCount ; ++i) {
									var offset = i * blockSize;
									var taskDestP = pdest + offset;
									var taskUmsP = pums + offset;
									var taskSize = (uint)Math.Min(blockSize, writable - offset);
									tasks[i] = Task.Run(() => {
										Unsafe.CopyBlock(taskDestP, taskUmsP, taskSize);
									});
								}
								Task.WaitAll(tasks);
								return writable;
							});
							return writable;

						}
						// synchronous
						ms.UsePointer(pums => {
							Unsafe.CopyBlock(pdest, pums,
								(uint)writable);
							return writable;
						});
						return writable;
					}

					var streamLength = long.MaxValue;
					try {
						streamLength = s.Length;
					} catch { /* oh well, blind copy */ }

					if (limit > MaxThreadBlockSize) {
						// dual-threaded overlapped block copy
						var blockSize = (int) Math.Min(MaxThreadBlockSize,
							Math.Min(limit, streamLength));
						var buf1 = ArrayPool<byte>.Shared
							.Rent(blockSize);
						var buf2 = ArrayPool<byte>.Shared
							.Rent(blockSize);
						long written = 0;
						var readTask1 = Task.FromResult(s.Read(buf1, 0, blockSize));
						if (readTask1.Result <= 0) return 0;
						for (; ;) {
							var readTask2 = s.ReadAsync(buf2, 0, blockSize);
							var readBytes1 = readTask1.Result;
							fixed (byte* psrc = &buf1[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) readBytes1);
							if (readBytes1 == 0 && readTask2.Result == 0) break;
							written += readBytes1;

							readTask1 = s.ReadAsync(buf1, 0, blockSize);
							var readBytes2 = readTask2.Result;
							fixed (byte* psrc = &buf2[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) readBytes2);
							if (readBytes2 == 0 && readTask1.Result == 0) break;
							written += readBytes2;
						}
						ArrayPool<byte>.Shared.Return(buf1);
						ArrayPool<byte>.Shared.Return(buf2);
						return written;
					}
					else {
						// synchronous
						var blockSize = (int) Math.Min(limit, streamLength);
						var buf = ArrayPool<byte>.Shared
							.Rent(blockSize);
						long written = 0;
						var read = s.Read(buf, 0, blockSize);
						if (read <= 0) return 0;
						do {
							fixed (byte* psrc = &buf[0])
								Unsafe.CopyBlock(pdest + written, psrc,
									(uint) read);
							written += read;
							read = s.Read(buf, 0, blockSize);
						} while (read > 0);
						ArrayPool<byte>.Shared.Return(buf);
						return written;
					}
				});
			}
		}
	}
}