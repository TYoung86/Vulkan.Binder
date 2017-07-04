using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Vulkan.Binder {
	[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
	public static class TaskManager {
		public static readonly TaskScheduler TaskScheduler
			= new MeticulousTaskScheduler();

		public static readonly TaskFactory TaskFactory
			= new TaskFactory(TaskScheduler);

		public static void RunSync(Task task) {
			task.RunSynchronously(TaskScheduler);
		}

		public static TResult RunSync<TResult>(Task<TResult> task) {
			task.RunSynchronously(TaskScheduler);
			return task.Result;
		}

		public static void RunSync(Func<Task> func, TaskCreationOptions taskCreationOptions)
			=> RunSync(func, default(CancellationToken), taskCreationOptions);

		public static void RunSync(Func<Task> func, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> RunSync(new Task<Task>(func, cancellationToken, taskCreationOptions)).Wait();

		public static TResult RunSync<TResult>(Func<Task<TResult>> func, TaskCreationOptions taskCreationOptions)
			=> RunSync(func, default(CancellationToken), taskCreationOptions);

		public static TResult RunSync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> RunSync(new Task<Task<TResult>>(func, cancellationToken, taskCreationOptions)).Result;

		public static void RunSync(Action action, TaskCreationOptions taskCreationOptions)
			=> RunSync(action, default(CancellationToken), taskCreationOptions);

		public static void RunSync(Action action, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> RunSync(new Task(action, cancellationToken, taskCreationOptions));

		public static TResult RunSync<TResult>(Func<TResult> func, TaskCreationOptions taskCreationOptions)
			=> RunSync(func, default(CancellationToken), taskCreationOptions);

		public static TResult RunSync<TResult>(Func<TResult> func, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> RunSync(new Task<TResult>(func, cancellationToken, taskCreationOptions));


		public static Task RunAsync(Task task) {
			task.Start(TaskScheduler);
			return task;
		}

		public static Task<TResult> RunAsync<TResult>(Task<TResult> task) {
			task.Start(TaskScheduler);
			return task;
		}


		public static Task RunAsync(Func<Task> func, TaskCreationOptions taskCreationOptions)
			=> RunAsync(func, default(CancellationToken), taskCreationOptions);

		public static Task RunAsync(Func<Task> func, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> TaskFactory.StartNew(func, cancellationToken, taskCreationOptions, TaskScheduler);

		public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> func, TaskCreationOptions taskCreationOptions)
			=> RunAsync(func, default(CancellationToken), taskCreationOptions);

		public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> func, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> TaskFactory.StartNew(() => func().Result, cancellationToken, taskCreationOptions, TaskScheduler);

		public static Task RunAsync(Action action, TaskCreationOptions taskCreationOptions)
			=> RunAsync(action, default(CancellationToken), taskCreationOptions);

		public static Task RunAsync(Action action, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> TaskFactory.StartNew(action, cancellationToken, taskCreationOptions, TaskScheduler);

		public static Task<TResult> RunAsync<TResult>(Func<TResult> func, TaskCreationOptions taskCreationOptions)
			=> RunAsync(func, default(CancellationToken), taskCreationOptions);

		public static Task<TResult> RunAsync<TResult>(Func<TResult> func, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> TaskFactory.StartNew(func, cancellationToken, taskCreationOptions, TaskScheduler);

		public static Task<TResult[]> CollectAsync<TResult>(IEnumerable<Task<TResult>> tasks)
			=> Task.WhenAll(tasks.Select(RunAsync));

		public static Task<TResult[]> CollectAsync<TResult>(params Task<TResult>[] tasks)
			=> CollectAsync((IEnumerable<Task<TResult>>) tasks);

		public static Task<TResult[]> CollectAsync<TResult>(IEnumerable<Func<Task<TResult>>> funcs)
			=> CollectAsync(funcs.Select(func => func()));

		public static Task<TResult[]> CollectAsync<TResult>(params Func<Task<TResult>>[] funcs)
			=> CollectAsync((IEnumerable<Func<Task<TResult>>>) funcs);

		public static Task<TResult[]> CollectAsync<T, TResult>(IEnumerable<T> items, Func<T, TResult> work, TaskCreationOptions taskCreationOptions)
			=> CollectAsync(items, work, default(CancellationToken), taskCreationOptions);

		public static Task<TResult[]> CollectAsync<T, TResult>(IEnumerable<T> items, Func<T, TResult> work, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> Task.WhenAll(items.Select(item => TaskFactory.StartNew(() => work(item), cancellationToken, taskCreationOptions, TaskScheduler)));
		

		public static Task<TResult[]> CollectAsync<T, TResult>(IEnumerable<T> items, Func<T, Task<TResult>> work, TaskCreationOptions taskCreationOptions)
			=> CollectAsync(items, work, default(CancellationToken), taskCreationOptions);

		public static Task<TResult[]> CollectAsync<T, TResult>(IEnumerable<T> items, Func<T, Task<TResult>> work, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = default(TaskCreationOptions))
			=> Task.WhenAll(items.Select(item => TaskFactory.StartNew(() => work(item).Result, cancellationToken, taskCreationOptions, TaskScheduler)));
	}
}