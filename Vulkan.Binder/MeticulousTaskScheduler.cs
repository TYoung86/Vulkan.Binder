using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
//using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Vulkan.Binder.Extensions;
using static System.Math;

namespace Vulkan.Binder {
	internal sealed class MeticulousTaskScheduler : TaskScheduler, IDisposable {
		private const int WatchdogWaitCount = 3;
		private const int WatchdogWaitTimeMs = 5;

		[ThreadStatic] private static bool _threadIsWorking;

		private readonly CancellationTokenSource _cts
			= new CancellationTokenSource();

		private readonly ConcurrentQueue<Task> _priorityTasks
			= new ConcurrentQueue<Task>();

		private readonly ConcurrentQueue<Task> _backloggedTasks
			= new ConcurrentQueue<Task>();

		private readonly ConcurrentQueue<Task> _longRunningTasks
			= new ConcurrentQueue<Task>();

		private volatile int _dequeueCount;

		private int _threadsWorking;

		private readonly LinkedList<Task> _dequeues
			= new LinkedList<Task>();

		private readonly Thread[] _threads;

		private readonly Thread _watchdog;
		
		private readonly ManualResetEventSlim _workReadyEvent
			= new ManualResetEventSlim(false);
		private readonly ManualResetEventSlim _reliefNotNeeded
			= new ManualResetEventSlim(false);

		public MeticulousTaskScheduler(int dop) {
			dop = Min(Max(1, dop), Environment.ProcessorCount);
			_threads = new Thread[dop];
			for (var i = 0 ; i < dop ; ++i)
				(_threads[i] = new Thread(WorkerAction) {IsBackground = true}).Start();
			MaximumConcurrencyLevel = dop;

			AssemblyLoadContext.Default.Unloading += ctx => Dispose();
			//AppDomain.CurrentDomain.DomainUnload += (s, e) => Dispose();

			(_watchdog = new Thread(WatchdogAction) {IsBackground = true}).Start();
		}

		public MeticulousTaskScheduler()
			: this(Environment.ProcessorCount - 1) {
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected override void QueueTask(Task task) {
			if ((task.CreationOptions & TaskCreationOptions.LongRunning) != default(TaskCreationOptions)) {
				_longRunningTasks.Enqueue(task);
				// untracked long-running task runners spawn and die
				new Thread(LongRunningWorkerAction) {IsBackground = false}.Start();
				return;
			}

			if (_threadIsWorking) {
				_priorityTasks.Enqueue(task);
				_workReadyEvent.Set();
				return;
			}

			_backloggedTasks.Enqueue(task);
			_workReadyEvent.Set();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WatchdogAction() {
			for (; ;) {
				try {
					try {
						_cts.Token.ThrowIfCancellationRequested();
					}
					catch (OperationCanceledException) {
						break;
					}
					var workerThreads = MaximumConcurrencyLevel;
					var lastTaskCount = _priorityTasks.Count;
					var reliefThreads = new LinkedList<Thread>();
					var waitedCount = 0;
					for (; ;) {
						// before sleeping, make sure relief threads are accounted
						var reliefThreadNode = reliefThreads.First;
						while (reliefThreadNode != null) {
							var reliefThread = reliefThreadNode.Value;
							if (!reliefThread.IsAlive) {
								var deadNode = reliefThreadNode;
								reliefThreadNode = reliefThreadNode.Next;
								reliefThreads.Remove(deadNode);
								continue;
							}
							reliefThreadNode = reliefThreadNode.Next;
						}
						// signal to let workers continue ig relief was active
						if (reliefThreads.Count == 0)
							_reliefNotNeeded.Set();


						try {
							_cts.Token.ThrowIfCancellationRequested();
						}
						catch (OperationCanceledException) {
							break;
						}

						Thread.Sleep(WatchdogWaitTimeMs);

						var dop = workerThreads;
						var waitCount = 0;
						for (var i = 0 ; i < workerThreads ; ++i) {
							switch (_threads[i].ThreadState) {
								case ThreadState.WaitSleepJoin:
									++waitCount;
									break;
							}
						}

						// account for relief threads in dop count
						reliefThreadNode = reliefThreads.First;
						while (reliefThreadNode != null) {
							var reliefThread = reliefThreadNode.Value;
							if (!reliefThread.IsAlive) {
								var deadNode = reliefThreadNode;
								reliefThreadNode = reliefThreadNode.Next;
								reliefThreads.Remove(deadNode);
								continue;
							}
							++dop;
							
							switch (reliefThread.ThreadState) {
								case ThreadState.WaitSleepJoin:
									++waitCount;
									break;
							}

							reliefThreadNode = reliefThreadNode.Next;
						}
						// if all the threads are active and waiting,
						// we need to spawn a relief thread if there are more
						// tasks left to process...
						var newTaskCount = _priorityTasks.Count;
						if (waitCount == dop
							&& _threadsWorking == dop
							&& lastTaskCount == newTaskCount) {

							if (_priorityTasks.IsEmpty
								|| _backloggedTasks.IsEmpty) {
								// if no tasks to queue, reset wait count...
								waitedCount = 0;
							} else if (waitedCount < WatchdogWaitCount)
								++waitedCount;
							else {
								// begin a relief bubble...
								waitedCount = 0;
								// reduce bubble burst effect when relief completes
								_reliefNotNeeded.Reset();
								var newReliefWorker
									= new Thread(ReliefWorkerAction);
								newReliefWorker.Start();
								reliefThreads.AddLast(newReliefWorker);
							}
						}
						lastTaskCount = newTaskCount;
					}
				}
				catch (OperationCanceledException) {
					break;
				}
				catch {
					//continue;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void LongRunningWorkerAction() {
			//Thread.BeginThreadAffinity();
			_threadIsWorking = true;
			while (_longRunningTasks.TryDequeue(out var task)) {
				try {
					if (!TryExecuteTask(task))
						_longRunningTasks.Enqueue(task);
				}
				catch {
					/* throw away */
				}
			}
			_threadIsWorking = false;
			//Thread.EndThreadAffinity();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ReliefWorkerAction() {
			try {
				var currentThread = Thread.CurrentThread;
				while (_priorityTasks.Count > 0) {
					//Thread.BeginThreadAffinity();
					currentThread.IsBackground = false;
					_threadIsWorking = true;
					Interlocked.Increment(ref _threadsWorking);
					Task task;
					bool worked = false;
					while (_priorityTasks.TryDequeue(out task))
						if (!IsDequeued(task))
							try {
								if (!TryExecuteTask(task)) {
									_priorityTasks.Enqueue(task);
									worked = true;
								}
							}
							catch {
								/* throw it away */
							}

					if (!worked && !_priorityTasks.TryPeek(out task)
						&& _backloggedTasks.TryDequeue(out task))
						if (!IsDequeued(task))
							try {
								if (!TryExecuteTask(task))
									_backloggedTasks.Enqueue(task);
							}
							catch {
								/* throw it away */
							}

					Interlocked.Decrement(ref _threadsWorking);
					_threadIsWorking = false;
					currentThread.IsBackground = true;
					//Thread.EndThreadAffinity();
				}
			}
			catch {
				/* just exit */
			}
			finally {
				_threadIsWorking = false;
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsDequeued(Task task) {
			if (_dequeueCount == 0)
				return false;
			lock (_dequeues) {
				var success = _dequeues.Remove(task);
				if (success) --_dequeueCount;
				return success;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WorkerAction() {
			try {
				var currentThread = Thread.CurrentThread;
				for (; ;) {
					try {
						_workReadyEvent.Wait(_cts.Token);
						_reliefNotNeeded.Wait(_cts.Token);
					}
					catch (OperationCanceledException) {
						break;
					}
					_workReadyEvent.Reset();
					//Thread.BeginThreadAffinity();
					currentThread.IsBackground = false;
					_threadIsWorking = true;
					Interlocked.Increment(ref _threadsWorking);
					Task task;

					bool priorityTasksAvailable;
					do {
						while (_priorityTasks.TryDequeue(out task)) {
							if (!IsDequeued(task))
								try {
									if (!TryExecuteTask(task))
										_priorityTasks.Enqueue(task);
								}
								catch {
									/* throw it away */
								}
							try {
								_reliefNotNeeded.Wait(_cts.Token);
							}
							catch (OperationCanceledException) {
								break;
							}
						}
						while (!(priorityTasksAvailable = _priorityTasks.TryPeek(out task))
								&& _backloggedTasks.TryDequeue(out task)) {
							if (!IsDequeued(task))
								try {
									if (!TryExecuteTask(task))
										_backloggedTasks.Enqueue(task);
								}
								catch {
									/* throw it away */
								}
							try {
								_reliefNotNeeded.Wait(_cts.Token);
							}
							catch (OperationCanceledException) {
								break;
							}
						}
					} while (priorityTasksAvailable);


					Interlocked.Decrement(ref _threadsWorking);
					_threadIsWorking = false;
					currentThread.IsBackground = true;
					//Thread.EndThreadAffinity();
				}
			}
			catch {
				/* just exit */
			}
			finally {
				_threadIsWorking = false;
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected override bool TryExecuteTaskInline(Task task, bool prevQueued) {
			if (!_threadIsWorking)
				return false;

			if (!prevQueued)
				return TryExecuteTask(task);

			if (!TryDequeue(task))
				return false;

			if (TryExecuteTask(task))
				return true;

			QueueTask(task);

			return false;
		}

		// Attempt to remove a previously scheduled task from the scheduler. 
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected override bool TryDequeue(Task task) {
			if (!_priorityTasks.Contains(task))
				return false;

			if (!_backloggedTasks.Contains(task))
				return false;

			lock (_dequeues) {
				++_dequeueCount;
				_dequeues.AddLast(task);
			}

			return true;
		}

		public override int MaximumConcurrencyLevel { get; }

		// Gets an enumerable of the tasks currently scheduled on this scheduler. 
		protected override IEnumerable<Task> GetScheduledTasks() {
			lock (_dequeues)
				return _priorityTasks
					.Union(_backloggedTasks)
					.Except(_dequeues)
					.ToImmutableArray();
		}

		public void Dispose() {
			_cts.Cancel();

			_workReadyEvent.Dispose();

			while (_priorityTasks.TryDequeue(out var _)) {
				/* clear */
			}
			while (_backloggedTasks.TryDequeue(out var _)) {
				/* clear */
			}

			while (_threadsWorking > 0)
				Thread.Sleep(10);

			//_watchdog.Abort();
			_watchdog.Join();
			for (var i = 0 ; i < MaximumConcurrencyLevel ; ++i) {
				_threads[i].Join();
				_threads[i] = null;
			}
		}
	}
}