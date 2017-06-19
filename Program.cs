using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;
using static Artilect.Vulkan.Binder.InteropAssemblyBuilder;

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

			VkXmlConsumer consumer;
			using (var client = new HttpClient()) {
				var vkXmlConsumerTask = Task.FromResult(client)
					.ContinueWith(t => {
						var vkXml = XDocument.Load(t.Result.GetStreamAsync(Cdn.VkXml).Result);
						return new VkXmlConsumer(vkXml);
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

				consumer = vkXmlConsumerTask.Result;
			}

			
			Console.WriteLine("Building Vulkan interop assembly.");

			var vkHeaderVersion = consumer.DefineTypes["VK_HEADER_VERSION"].LastNode.ToString().Trim();

			var asmBuilder = new InteropAssemblyBuilder("Vulkan", "1.0." + vkHeaderVersion);

			var knownTypes = ImmutableDictionary.CreateBuilder<string, KnownType>();

			knownTypes.AddRange(
				consumer.EnumTypes.Keys.Select(k => new KeyValuePair<string, KnownType>
					(k, KnownType.Enum)));

			knownTypes.AddRange(
				consumer.BitmaskTypes.Keys.Select(k => new KeyValuePair<string, KnownType>
					(k, KnownType.Bitmask)));

			asmBuilder.KnownTypes = knownTypes.ToImmutable();

			asmBuilder.ParseHeader("vulkan.h");

			asmBuilder.Compile();

			var asm = asmBuilder.Save(startingDirectory);

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