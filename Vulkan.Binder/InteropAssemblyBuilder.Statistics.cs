using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private readonly ConcurrentDictionary<string, long> _statistics
			= new ConcurrentDictionary<string, long>();

		// ReSharper disable once UnassignedReadonlyField // yes it is assigned...
		public readonly IReadOnlyDictionary<string, long> Statistics;

		private void IncrementStatistic(string name) {
			_statistics.AddOrUpdate(name, 1, (k, v) => v + 1);
		}
	}
}