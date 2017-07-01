using System;
using System.Collections.Concurrent;
using Mono.Cecil;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		public void BuildTypeDefinitions() {
			var definitionFuncs = DefinitionFuncs;

			var definitionFuncCount = definitionFuncs.Count;
			var totalDefinitionFuncCount = definitionFuncCount;
			var successfulDefinitionCount = 0;

			var retryDefinitionFuncs = new ConcurrentStack<Func<TypeDefinition[]>>();

			var exceptions = new ConcurrentQueue<Exception>();

			do {
				while (definitionFuncs.TryPop(out var definitionFunc)) {
					if (definitionFunc == null)
						continue;
					try {
						definitionFunc();
						ReportProgress("Building type definitions",
							successfulDefinitionCount++, totalDefinitionFuncCount);
					}
					catch (InvalidProgramException) {
						throw;
					}
					catch (Exception ex) {
						exceptions.Enqueue(ex);
						retryDefinitionFuncs.Push(definitionFunc);
					}
				}

				var retryDefinitionFuncCount = retryDefinitionFuncs.Count;

				if (retryDefinitionFuncCount == 0) break;

				if (definitionFuncCount == retryDefinitionFuncCount)
					throw new AggregateException(exceptions);

				exceptions = new ConcurrentQueue<Exception>();
				definitionFuncCount = retryDefinitionFuncCount;

				var temp = definitionFuncs;
				definitionFuncs = retryDefinitionFuncs;
				retryDefinitionFuncs = temp;
			} while (definitionFuncCount > 0);
			ReportProgress("Building type definitions",
				successfulDefinitionCount, totalDefinitionFuncCount);

			//Delegates.CreateType();
		}
	}
}