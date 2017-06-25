﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public static class Program {
		public const string VkManBasePath = "https://www.khronos.org/registry/vulkan/specs/1.0/man/html/";

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

		private static Task<KeyValuePair<string, Stream>> CreateHttpFetchTaskAsync(string filePath) {
			var fileName = Path.GetFileName(filePath);
			Console.WriteLine("Creating Fetch: {0}", fileName);

			return Task.Run(async () => new KeyValuePair<string, Stream>(
				fileName,
				await Task.Run(async () => {
					var fetchStream = await HttpClient.GetStreamAsync(filePath).ConfigureAwait(false);
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
			TaskManager.RunSync(MainAsync);
		}

		public static readonly HttpClient HttpClient = new HttpClient();
		private static TypeDefinition _cgaTd;
		private static TypeDefinition _mcdTd;

		public static async Task MainAsync() {
			var tempDirName = $"{typeof(Program).Namespace}.{Process.GetCurrentProcess().Id}";
			var tempPath = Path.Combine(Path.GetTempPath(), tempDirName);
			var workDirectory = Directory.CreateDirectory(tempPath);
			var startingDirectory = Environment.CurrentDirectory;
			Environment.CurrentDirectory = workDirectory.FullName;

			var xmlTask = Task.Run(async () => new VkXmlConsumer(XDocument.Load(
				await HttpClient.GetStreamAsync(Cdn.VkXml).ConfigureAwait(false))));


			var headersSaved = Task.WhenAll(await Task.Run(async () => {
				var streams = (await Task.WhenAll(new[] {
						Cdn.StdDef,
						Cdn.StdInt,
						Cdn.StdDefMaxAlignT,
						Cdn.VkPlatform,
						Cdn.Vulcan
					}.Select(path => CreateHttpFetchTaskAsync(path))).ConfigureAwait(false))
					.ToImmutableDictionary();
				return streams.Select(kvp => {
					var stream = kvp.Value;
					var fileStream = File.Create(Path.Combine(workDirectory.FullName, kvp.Key));
					return stream.CopyToAsync(fileStream)
						.ContinueWith(x => fileStream.FlushAsync())
						.ContinueWith(x => {
							fileStream.Dispose();
							stream.Dispose();
						});
				});
			}).ConfigureAwait(false));


			var xml = xmlTask.Result;

			Console.WriteLine("Building Vulkan interop assembly.");

			var vkHeaderVersion = xml.DefineTypes["VK_HEADER_VERSION"].LastNode.ToString().Trim();

			var asmBuilder = new InteropAssemblyBuilder("Vulkan", "1.0." + vkHeaderVersion);

			var knownTypes = ImmutableDictionary.CreateBuilder<string, InteropAssemblyBuilder.KnownType>();
			var typeRedirs = ImmutableDictionary.CreateBuilder<string, string>();

			foreach (var enumType in xml.EnumTypes) {
				knownTypes.Add(enumType.Key, InteropAssemblyBuilder.KnownType.Enum);
				var requires = enumType.Value.Attribute("requires")?.Value;
				if (requires != null)
					typeRedirs.Add(requires, enumType.Key);
			}

			foreach (var bitmaskType in xml.BitmaskTypes) {
				knownTypes.Add(bitmaskType.Key, InteropAssemblyBuilder.KnownType.Bitmask);
				var requires = bitmaskType.Value.Attribute("requires")?.Value;
				if (requires != null)
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

			foreach (var command in xml.Commands) {
				typeRedirs.Add("PFN_" + command.Key, command.Key);
			}

			// handle remaining FlagBits -> Flags references that they missed
			var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
			var verifyingKnownTypesIterator = knownTypes.Keys.ToArray().Where(n => knownTypes.ContainsKey(n));
			foreach (var name in verifyingKnownTypesIterator) {
				if (compareInfo.IndexOf(name, "Flags", CompareOptions.Ordinal) != -1) {
					var otherName = name.Replace("Flags", "FlagBits");
					if (!knownTypes.TryGetValue(otherName, out var otherType))
						continue;
					if (otherType == InteropAssemblyBuilder.KnownType.Bitmask)
						knownTypes[name] = InteropAssemblyBuilder.KnownType.Bitmask;
					knownTypes.Remove(otherName);
					if (!typeRedirs.ContainsKey(otherName))
						typeRedirs.Add(otherName, name);
				}
				else if (compareInfo.IndexOf(name, "FlagBits", CompareOptions.Ordinal) != -1) {
					var otherName = name.Replace("FlagBits", "Flags");
					if (!knownTypes.TryGetValue(name, out var knownType))
						continue;
					if (knownType == InteropAssemblyBuilder.KnownType.Bitmask)
						knownTypes[otherName] = InteropAssemblyBuilder.KnownType.Bitmask;
					knownTypes.Remove(name);
					if (!typeRedirs.ContainsKey(name))
						typeRedirs.Add(name, otherName);
				}
			}

			foreach (var handle in xml.HandleTypes.Keys) {
				typeRedirs.Add(handle + "_T", handle);
			}

			foreach (var funcName in xml.Commands.Keys) {
				if (funcName.StartsWith("PFN_"))
					throw new NotImplementedException();
				typeRedirs.Add("PFN_" + funcName, funcName);
			}

			// for each command that has successcodes="VK_SUCCESS,VK_INCOMPLETE"
			// with last parameter optional="true" and second to last parameter optional="false,true"
			// create something to handle back-to-back calls for count and alloc array of last parameter type

			asmBuilder.KnownTypes = knownTypes.ToImmutable();
			asmBuilder.TypeRedirects = typeRedirs.ToImmutable();

			headersSaved.Wait();

			asmBuilder.ParseHeader("vulkan.h");

			asmBuilder.Compile();

			// create automatic runtime linker bound to module initializer
			var moduleType = asmBuilder.Module.GetType("<Module>");
			var staticLinkType = asmBuilder.Module.DefineType("Vulkan", TypeAttributes.Abstract | TypeAttributes.Sealed);
			var staticLinkInit = staticLinkType.DefineConstructor(MethodAttributes.Static | MethodAttributes.Assembly);
			staticLinkInit.GenerateIL(il => {
				// 
				il.Emit(OpCodes.Ret);
			});

			var moduleInit = moduleType.DefineConstructor(MethodAttributes.Static | MethodAttributes.Assembly);
			moduleInit.GenerateIL(il => {
				il.Emit(OpCodes.Call, staticLinkInit);
				il.Emit(OpCodes.Ret);
			});

			var asm = asmBuilder.Save(startingDirectory);

			_cgaTd = typeof(CompilerGeneratedAttribute).Import(asmBuilder.Module).Resolve();
			_mcdTd = typeof(MulticastDelegate).Import(asmBuilder.Module).Resolve();

			var compilerGeneratedTypes = asmBuilder.Module.Types
				.Select(et => et.Resolve())
				.Where(et => et.CustomAttributes.Any(ca => ca.AttributeType.Is(_cgaTd)))
				.ToArray();

			var xdoc = new XDocument(
				new XElement("doc",
					new XElement("assembly",
						new XElement("name",
							new XText(asmBuilder.Name.Name)
						)
					),
					new XElement("members",
						await TaskManager.CollectAsync(compilerGeneratedTypes,
							FetchAndParseHtmlDocAsync).ConfigureAwait(false)
					)
				)
			);

			xdoc.Save(Path.Combine(startingDirectory, "Vulkan.xml"));

			if (!asm.Exists)
				throw new NotImplementedException();

			Console.WriteLine("Provided product artifacts.");

			foreach (var stat in asmBuilder.Statistics)
				Console.WriteLine($"{stat.Value} {stat.Key}.");

			Console.WriteLine("Cleaning up temporary artifacts.");

			Environment.CurrentDirectory = startingDirectory;
			workDirectory.Delete(true);

			Console.WriteLine("Done.");

			if (!Environment.UserInteractive) return;

			Console.WriteLine("Press any key to exit.");
			Console.ReadKey(true);
		}
		
		private static async Task<XElement> FetchAndParseHtmlDocAsync(TypeDefinition et) {
			var name = et.FullName;
			var typeDesc = new XElement("member", new XAttribute("name", $"T:{name}"));
			if (et.IsInterface) {
				// strip 'I' prefix
				if (name[0] != 'I')
					throw new NotImplementedException();
				name = name.Substring(1);
				await FetchAndParseHtmlDocAsync(name, typeDesc).ConfigureAwait(false);
				return typeDesc;
			}
			var baseType = et.BaseType;
			if (baseType.Is(_mcdTd)) {
				// function
				await FetchAndParseHtmlDocAsync(name, typeDesc).ConfigureAwait(false);
				return typeDesc;
			}
			var @interface = et.Interfaces.FirstOrDefault();
			if (@interface != null && !@interface.InterfaceType.IsGenericInstance ) {
				// TODO: inherit from interface
				typeDesc.Add(new XElement("seealso", new XAttribute("cref", $"T:{@interface.InterfaceType.FullName}")));
				return typeDesc;
			}
			// handle or struct
			await FetchAndParseHtmlDocAsync(name, typeDesc).ConfigureAwait(false);
			return typeDesc;
		}
		private static async Task FetchAndParseHtmlDocAsync(string name, XContainer typeDesc) {
			var hdoc = new HtmlDocument {OptionOutputAsXml = true};
			Console.WriteLine("Fetching doc for: {0}", name);
			Stream stream;
			try {
				stream = await HttpClient.GetStreamAsync
					(VkManBasePath + name + ".html").ConfigureAwait(false);
			}
			catch (HttpRequestException ex) {
				// 404 or such
				Console.WriteLine("No doc for: {0}", name);
				return;
			}
			hdoc.Load(stream);
			Console.WriteLine("Fetched doc for: {0}", name);

			// summary
			var nameParagraph = hdoc.QuerySelector("#header p")?.InnerText;
			if (nameParagraph != null) {
				var nameParagraphParts = nameParagraph.Split(new[] {'-'}, 2);

				if (nameParagraphParts.Length != 2)
					throw new NotImplementedException();

				typeDesc.Add(new XElement("summary",
					new XText(nameParagraphParts[1])));
			}

			// remarks
			XElement remarksElem = new XElement("remarks");
			var descSection = hdoc.QuerySelector("#_description + .sectionbody");
			if (descSection != null) {
				await WriteHtmlNodeToXmlElementAsync(descSection, remarksElem).ConfigureAwait(false);
			}

			// c spec, copy into remarks
			var specSection = hdoc.QuerySelector("#content #_c_specification + .sectionbody");
			if (specSection != null)
				await WriteHtmlNodeToXmlElementAsync(specSection, remarksElem).ConfigureAwait(false);

			if ( remarksElem.DescendantNodes().Any() )
				typeDesc.Add(remarksElem);

			// see alsos
			var seeAlsos = hdoc.QuerySelectorAll("#_seealso + .sectionbody a");
			foreach (var seeAlso in seeAlsos)
				typeDesc.Add(new XElement("seealso", new XAttribute("cref", $"T:{seeAlso}")));
		}
		private static async Task WriteHtmlNodeToXmlElementAsync(HtmlNode hn, XElement xe) {
			try {
				using (var ms = new MemoryStream()) {
					var sw = new StreamWriter(ms);
					await sw.WriteAsync("<div>").ConfigureAwait(false);
					hn.WriteContentTo(sw);
					await sw.WriteAsync("</div>").ConfigureAwait(false);
					await sw.FlushAsync().ConfigureAwait(false);
					ms.Seek(0, SeekOrigin.Begin);

					var sr = new StreamReader(ms);
					xe.Add(XElement.Load(sr));
					sw.Dispose();
				}
			}
			catch (XmlException ex) {
				// ?!
			}
	
		}
	}
}