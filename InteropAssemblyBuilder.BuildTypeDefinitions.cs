using System;
using System.Collections.Concurrent;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		public void BuildTypeDefinitions() {
			var definitionFuncs = DefinitionFuncs;

			var definitionFuncCount = definitionFuncs.Count;

			var retryDefinitionFuncs = new ConcurrentBag<Func<Type[]>>();

			var exceptions = new ConcurrentBag<Exception>();

			do {
				while (definitionFuncs.TryTake(out var definitionFunc)) {
					try {
						definitionFunc();
					}
					catch (InvalidProgramException) {
						throw;
					}
					catch (Exception ex) {
						exceptions.Add(ex);
						retryDefinitionFuncs.Add(definitionFunc);
					}
				}

				var retryDefinitionFuncCount = retryDefinitionFuncs.Count;

				if (retryDefinitionFuncCount == 0) break;

				if (definitionFuncCount == retryDefinitionFuncCount)
					throw new AggregateException(exceptions);

				exceptions = new ConcurrentBag<Exception>();
				definitionFuncCount = retryDefinitionFuncCount;

				var temp = definitionFuncs;
				definitionFuncs = retryDefinitionFuncs;
				retryDefinitionFuncs = temp;
			} while (definitionFuncCount > 0);

			//Delegates.CreateType();
		}
	}
}