using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
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

			VkXmlConsumer xml;
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

				xml = vkXmlConsumerTask.Result;
			}

			
			Console.WriteLine("Building Vulkan interop assembly.");

			var vkHeaderVersion = xml.DefineTypes["VK_HEADER_VERSION"].LastNode.ToString().Trim();

			var asmBuilder = new InteropAssemblyBuilder("Vulkan", "1.0." + vkHeaderVersion);
			
			var knownTypes = ImmutableDictionary.CreateBuilder<string, KnownType>();
			var typeRedirs = ImmutableDictionary.CreateBuilder<string, string>();

			foreach (var enumType in xml.EnumTypes) {
				knownTypes.Add( enumType.Key, KnownType.Enum);
				var requires = enumType.Value.Attribute("requires")?.Value;
				if ( requires != null )
					typeRedirs.Add(requires, enumType.Key);
			}

			foreach (var bitmaskType in xml.BitmaskTypes) {
				knownTypes.Add( bitmaskType.Key, KnownType.Bitmask);
				var requires = bitmaskType.Value.Attribute("requires")?.Value;
				if ( requires != null )
					typeRedirs.Add(requires, bitmaskType.Key);
			}
			
			// make KnownTypes consistent with TypeRedirects
			foreach (var typeRedir in typeRedirs) {
				if (knownTypes.TryGetValue(typeRedir.Key, out var knownType)) {
					knownTypes.Remove(typeRedir.Key);
					if (!knownTypes.ContainsKey(typeRedir.Value)) {
						knownTypes.Add(typeRedir.Value, knownType);
					}
				}
			}

			// handle remaining FlagBits -> Flags references that they missed
			var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
			var verifyingKnownTypesIterator = knownTypes.Keys.ToArray().Where(n => knownTypes.ContainsKey(n));
			foreach (var name in verifyingKnownTypesIterator) {
				if (compareInfo.IndexOf(name, "Flags", CompareOptions.Ordinal) != -1) {
					var otherName = name.Replace("Flags", "FlagBits");
					if (!knownTypes.TryGetValue(otherName, out var otherType))
						continue;
					if (otherType == KnownType.Bitmask)
						knownTypes[name] = KnownType.Bitmask;
					knownTypes.Remove(otherName);
					if (!typeRedirs.ContainsKey(otherName))
						typeRedirs.Add(otherName, name);
				} else if (compareInfo.IndexOf(name, "FlagBits", CompareOptions.Ordinal) != -1) {
					var otherName = name.Replace("FlagBits", "Flags");
					if (!knownTypes.TryGetValue(name, out var knownType))
						continue;
					if (knownType == KnownType.Bitmask)
						knownTypes[otherName] = KnownType.Bitmask;
					knownTypes.Remove(name);
					if (!typeRedirs.ContainsKey(name))
						typeRedirs.Add(name, otherName);
				}
			}

			foreach (var handle in xml.HandleTypes.Keys) {
				typeRedirs.Add(handle+"_T",handle);
			}

			
			asmBuilder.KnownTypes = knownTypes.ToImmutable();
			asmBuilder.TypeRedirects = typeRedirs.ToImmutable();

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