using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Artilect.Vulkan.Binder {
	public static class Program {
		public static class Cdn {
			private const string CdnBasePath = "https://cdn.rawgit.com/";

			private const string LlvmClang = CdnBasePath + "llvm-mirror/clang/release_40";

			public const string StdDef = LlvmClang + "/lib/Headers/stddef.h";
			public const string StdInt = LlvmClang + "/lib/Headers/stdint.h";
			public const string StdDefMaxAlignT = LlvmClang + "/lib/Headers/__stddef_max_align_t.h";

			private const string VulkanDocs = CdnBasePath + "KhronosGroup/Vulkan-Docs/1.0";

			public const string VkPlatform = VulkanDocs + "/src/vulkan/vk_platform.h";
			public const string Vulcan = VulkanDocs + "/src/vulkan/vulkan.h";
			public const string VkXml = VulkanDocs + "/src/spec/vk.xml";
		}

		private static Task<KeyValuePair<string, Stream>> CreateHttpFetchTaskAsync(HttpClient client, string filePath) {
			var fileName = Path.GetFileName(filePath);
			Console.WriteLine("Creating Fetch: {0}", fileName);

			return Task.Run(async () => new KeyValuePair<string, Stream>(
				fileName,
				await Task.Run(async () => {
					var fetchStream = await client.GetStreamAsync(filePath).ConfigureAwait(false);
					Console.WriteLine("Fetching: {0}", fileName);
					MemoryStream ms;
					try {
						ms = new MemoryStream(new byte[fetchStream.Length], true);
					}
					catch (NotSupportedException) {
						ms = new MemoryStream();
					}
					await fetchStream.CopyToAsync(ms).ConfigureAwait(false);
					ms.Position = 0;
					Console.WriteLine("Fetched: {0}", fileName);
					return ms;
				}).ConfigureAwait(false)
			));
		}

		public static void Main() {
			var tempDirName = $"{typeof(Program).Namespace}.{Process.GetCurrentProcess().Id}";
			var tempPath = Path.Combine(Path.GetTempPath(), tempDirName);
			var workDirectory = Directory.CreateDirectory(tempPath);
			var startingDirectory = Environment.CurrentDirectory;
			Environment.CurrentDirectory = workDirectory.FullName;

			using (var client = new HttpClient()) {
				var vkXmlConsumerTask = Task.FromResult(client)
					.ContinueWith(async t => {
						var vkXml = XDocument.Load(await t.Result.GetStreamAsync(Cdn.VkXml).ConfigureAwait(false));
						var consumer = new VkXmlConsumer(vkXml);
					});

				var headerHttpFetchTasks = new[] {
					Cdn.StdDef, Cdn.StdInt, Cdn.StdDefMaxAlignT, Cdn.VkPlatform, Cdn.Vulcan
				}.Select(path => CreateHttpFetchTaskAsync(client, path));


				var cppSharpTask = Task.WhenAll(headerHttpFetchTasks).ContinueWith(t => {
					var streams = t.Result.ToImmutableDictionary();
					var copyTaskIndex = 0;
					var copyTasks = new Task[streams.Count];
					foreach (var kvp in streams) {
						var name = kvp.Key;
						var stream = kvp.Value;
						var fileStream = File.Create(Path.Combine(workDirectory.FullName, name));
						var copyTask = stream.CopyToAsync(fileStream)
							.ContinueWith(x => fileStream.FlushAsync())
							.ContinueWith(x => {
								fileStream.Dispose();
								stream.Dispose();
							});
						copyTasks[copyTaskIndex++] = copyTask;
					}
					Task.WaitAll(copyTasks);
				});

				Task.WaitAll(
					vkXmlConsumerTask,
					cppSharpTask
				);
			}

			
			Console.WriteLine("Building Vulkan interop assembly.");

			var asmBuilder = new InteropAssemblyBuilder("Vulkan");

			asmBuilder.ParseHeader("vulkan.h");

			var asm = asmBuilder.CompileAndSave(startingDirectory);

			if (!asm.Exists)
				throw new NotImplementedException();

			Console.WriteLine("Provided product artifacts.");

			foreach (var stat in asmBuilder.Statistics)
				Console.WriteLine($"{stat.Value} {stat.Key}.");

			Console.WriteLine("Cleaning up temporary artifacts.");
			
			Environment.CurrentDirectory = startingDirectory;
			workDirectory.Delete(true);

			Console.WriteLine("Done.");

			if (Environment.UserInteractive) {
				Console.WriteLine("Press any key to exit.");
				Console.ReadKey(true);
			}
		}
	}
}