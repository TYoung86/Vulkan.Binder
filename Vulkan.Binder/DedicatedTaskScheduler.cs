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
	internal sealed class DedicatedTaskScheduler : TaskScheduler, IDisposable {
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

		public DedicatedTaskScheduler(int dop) {
			dop = Min(Max(1, dop), Environment.ProcessorCount);
			_threads = new Thread[dop];
			for (var i = 0 ; i < dop ; ++i)
				(_threads[i] = new Thread(WorkerAction) {IsBackground = true}).Start();
			MaximumConcurrencyLevel = dop;

			AssemblyLoadContext.Default.Unloading += ctx => Dispose();
			//AppDomain.CurrentDomain.DomainUnload += (s, e) => Dispose();

			(_watchdog = new Thread(WatchdogAction) {IsBackground = true}).Start();
		}

		public DedicatedTaskScheduler()
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
					_cts.Token.ThrowIfCancellationRequested();
					var workerThreads = MaximumConcurrencyLevel;
					var dop = workerThreads;
					var lastTaskCount = _priorityTasks.Count;
					var reliefThreads = new LinkedList<Thread>();
					var waitedCount = 0;
					for (; ;) {
						Thread.Sleep(5);
						dop = workerThreads;
						var waitCount = 0;
						for (var i = 0 ; i < workerThreads ; ++i) {
							if (_threads[i].ThreadState == ThreadState.WaitSleepJoin)
								++waitCount;
						}

						var reliefThreadNode = reliefThreads.First;
						while (reliefThreadNode != null) {
							var reliefThread = reliefThreadNode.Value;
							if (!reliefThread.IsAlive) {
								var deadNode = reliefThreadNode;
								reliefThreadNode = reliefThreadNode.Next;
								reliefThreads.Remove(deadNode);
								continue;
							}
							++dop;
							if (reliefThread.ThreadState == ThreadState.WaitSleepJoin)
								++waitCount;
							reliefThreadNode = reliefThreadNode.Next;
						}

						var newTaskCount = _priorityTasks.Count;
						if (waitCount == dop && lastTaskCount == newTaskCount) {
							if (waitedCount < 3)
								++waitedCount;
							else {
								waitedCount = 0;
								var newReliefWorker = new Thread(ReliefWorkerAction);
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

					while (_priorityTasks.TryDequeue(out task))
						if (!IsDequeued(task))
							try {
								if (!TryExecuteTask(task))
									_priorityTasks.Enqueue(task);
							}
							catch {
								/* throw it away */
							}

					if (!_priorityTasks.TryPeek(out task)
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
					_workReadyEvent.Wait(_cts.Token);
					_workReadyEvent.Reset();
					//Thread.BeginThreadAffinity();
					currentThread.IsBackground = false;
					_threadIsWorking = true;
					Interlocked.Increment(ref _threadsWorking);
					Task task;

					bool priorityTasksAvailable;
					do {
						while (_priorityTasks.TryDequeue(out task))
							if (!IsDequeued(task))
								try {
									if (!TryExecuteTask(task))
										_priorityTasks.Enqueue(task);
								}
								catch {
									/* throw it away */
								}
						while (!(priorityTasksAvailable = _priorityTasks.TryPeek(out task))
								&& _backloggedTasks.TryDequeue(out task))
							if (!IsDequeued(task))
								try {
									if (!TryExecuteTask(task))
										_backloggedTasks.Enqueue(task);
								}
								catch {
									/* throw it away */
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

			for (var i = 0 ; i < MaximumConcurrencyLevel ; ++i)
				_threads[i] = null;
		}
	}
}