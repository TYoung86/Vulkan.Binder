﻿using System;
using System.Collections;
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
using System.Xml.XPath;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Interop;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public static class Program {
		public const string VkManBasePath = "https://www.khronos.org/registry/vulkan/specs/1.0/man/html/";
		public static readonly Uri VkManBasePathUri = new Uri(VkManBasePath);
		public static readonly string LocalVulkanHtmlManPages = Environment.GetEnvironmentVariable("VULKAN_HTMLMANPAGES");

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
			WriteLine("Creating Fetch: {0}", fileName);

			return Task.Run(async () => new KeyValuePair<string, Stream>(
				fileName,
				await Task.Run(async () => {
					var fetchStream = await HttpClient.GetStreamAsync(filePath).ConfigureAwait(false);
					WriteLine("Fetching: {0}", fileName);
					MemoryStream ms;
					try {
						ms = new MemoryStream(new byte[fetchStream.Length], true);
					}
					catch (NotSupportedException) {
						ms = new MemoryStream();
					}
					await fetchStream.CopyToAsync(ms).ConfigureAwait(false);
					ms.Position = 0;
					WriteLine("Fetched: {0}", fileName);
					return ms;
				}).ConfigureAwait(false)
			));
		}

		private static StreamWriter LogWriter
			= new StreamWriter(new FileStream(
				$"{typeof(Program).Namespace}.{Process.GetCurrentProcess().Id}.log",
				FileMode.Append, FileAccess.Write, FileShare.Read, 32768)) {
				AutoFlush = true, NewLine = "\r\n"
			};
		
		public static void LogWriteLine(string format, params object[] args) {
			LogWriter.WriteLine(format, args);
		}

		public static void WriteLine(string format, params object[] args) {
			var line = string.Format(format, args);
			LogWriteLine(line);
			Console.WriteLine(line);
		}
		
		public static void LogWrite(string format, params object[] args) {
			LogWriter.Write(format, args);
		}
		public static void Write(string format, params object[] args) {
			var line = string.Format(format, args);
			LogWrite(line);
			Console.Write(line);
		}

		public static void Main() {
			TaskManager.RunSync(MainAsync);
		}

		public static readonly HttpClient HttpClient = new HttpClient();

		public static async Task MainAsync() {
			var tempDirName = $"{typeof(Program).Namespace}.{Process.GetCurrentProcess().Id}";
			var tempPath = Path.Combine(Path.GetTempPath(), tempDirName);
			if ( Directory.Exists(tempPath) ) Directory.Delete(tempPath, true);
			var workDirectory = Directory.CreateDirectory(tempPath);
			var startingDirectory = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(workDirectory.FullName);

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
			
			Console.WriteLine();
			LogWriteLine("Building Vulkan interop assembly.");

			var vkHeaderVersion = xml.DefineTypes["VK_HEADER_VERSION"].LastNode.ToString().Trim();

			_asmBuilder = new InteropAssemblyBuilder("Vulkan", "1.0." + vkHeaderVersion);
			string lastState = null;
			_asmBuilder.ProgressReportFunc = (state, done, total) => {
				if (state != lastState) {
					Console.WriteLine();
					LogWriteLine("");
					LogWrite(state);
				}
				if (total <= 0 || done < 0)
					Console.WriteLine(state);
				else {
					var progress = (double) done / total;
					Console.Write("{0} {1:P} ({2}/{3})\r",
						state, progress, done, total);
					LogWrite(".");
				}

				lastState = state;
			};

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

			_asmBuilder.KnownTypes = knownTypes.ToImmutable();
			_asmBuilder.TypeRedirects = typeRedirs.ToImmutable();
			
			headersSaved.Wait();
			
			Console.WriteLine();
			WriteLine("Parsing headers.");
			_asmBuilder.ParseHeader("vulkan.h");
			
			Console.WriteLine();
			WriteLine("Compiling headers.");

			_asmBuilder.Compile();

			Console.WriteLine();
			WriteLine("Adding customizations...");

			// create automatic runtime linker bound to module initializer
			var moduleType = _asmBuilder.Module.GetType("<Module>");
			var staticLinkType = _asmBuilder.Module.DefineType("Vulkan",
				TypeAttributes.Abstract
				| TypeAttributes.Sealed
				| TypeAttributes.Public
			);
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
			
			Console.WriteLine();
			WriteLine("Saving constructed assembly.");
			var asm = _asmBuilder.Save(startingDirectory);

			var compilerGeneratedTypes = _asmBuilder.Module.Types
				.Select(et => et.Resolve())
				.Where(et => et.CustomAttributes.Any(ca => ca.AttributeType.Is(_asmBuilder.BinderGeneratedAttributeType)))
				.ToArray();
			
			Console.WriteLine();
			WriteLine($"Generating XML documentation for {compilerGeneratedTypes.Length} members.");
			var xdoc = new XDocument(
				new XElement("doc",
					new XElement("assembly",
						new XElement("name",
							new XText(_asmBuilder.Name.Name)
						)
					),
					new XElement("members",
						await TaskManager.CollectAsync(compilerGeneratedTypes,
							FetchAndParseHtmlDocAsync).ConfigureAwait(false)
					)
				)
			);
			
			Console.WriteLine();
			WriteLine("Saving XML documentation.");

			using (var fs = File.Open(Path.Combine(startingDirectory, "Vulkan.xml"),FileMode.Create))
				xdoc.Save(fs);

			if (!asm.Exists)
				throw new NotImplementedException();
			
			Console.WriteLine();
			WriteLine("Provided product artifacts.");

			foreach (var stat in _asmBuilder.Statistics)
				Console.WriteLine($"{stat.Value} {stat.Key}.");
			
			Console.WriteLine();
			WriteLine("Cleaning up temporary artifacts.");

			Directory.SetCurrentDirectory(startingDirectory);
			try {
				workDirectory.Delete(true);
			}
			catch (IOException ex) {
				Console.WriteLine();
				WriteLine($"Cleaning up failed: {ex.Message}.");
			}
			
			Console.WriteLine();
			WriteLine("Done.");
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
			if (baseType.Is(_asmBuilder.MulticastDelegateType)) {
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
			WriteLine("Fetching doc for: {0}", name);
			Stream stream;
			try {
				if (LocalVulkanHtmlManPages != null) {
					var localHtmlPath = Path.Combine(LocalVulkanHtmlManPages, name + ".html");
					stream = File.Exists(localHtmlPath)
						? File.OpenRead(localHtmlPath)
						: throw new FileNotFoundException(localHtmlPath);
				}
				else {
					stream = await HttpClient.GetStreamAsync
						(VkManBasePath + name + ".html").ConfigureAwait(false);
				}
			}
			catch (FileNotFoundException) {
				WriteLine("No local doc for: {0}", name);
				return;
			}
			catch (HttpRequestException) {
				WriteLine("No remote doc for: {0}", name);
				return;
			}
			hdoc.Load(stream);
			WriteLine("Fetched doc for: {0}", name);

			// summary
			var nameParagraph = hdoc.QuerySelector("#header p")?.InnerText;
			if (nameParagraph != null) {
				var nameParagraphParts = nameParagraph.Split(new[] {'-'}, 2);

				if (nameParagraphParts.Length != 2)
					throw new NotImplementedException();

				typeDesc.Add(new XElement("summary",
					new XText(nameParagraphParts[1].Trim())));
			}

			// remarks
			var remarksElem = new XElement("remarks");
			var descSection = hdoc.QuerySelector("#_description + .sectionbody");
			if (descSection != null) {
				WriteHtmlNodeToXmlElement(descSection, remarksElem);
			}

			// c spec, copy into remarks
			var specSection = hdoc.QuerySelector("#content #_c_specification + .sectionbody");
			if (specSection != null)
				WriteHtmlNodeToXmlElement(specSection, remarksElem);

			if ( remarksElem.Nodes().Any() )
				typeDesc.Add(remarksElem);

			// see alsos
			var seeAlsos = hdoc.QuerySelectorAll("#_see_also + .sectionbody a");
			foreach (var seeAlso in seeAlsos) {
				string cref = null;
				var href = seeAlso.GetAttributeValue("href",null);
				if (href == null)
					continue;
				if (href.StartsWith("#")) {
					continue;
				}
				if (href.StartsWith(".")) {
					cref = $"!:{new Uri(VkManBasePathUri, href).AbsoluteUri}";
				}
				else if (href.StartsWith("http://")||href.StartsWith("https://")) {
					cref = $"!:{new Uri(VkManBasePathUri, href).AbsoluteUri}";
				}
				else if (href.StartsWith("/")) {
					throw new NotImplementedException();
				}
				else if (href.EndsWith(".html")) {
					cref = $"T:{href.Substring(0, href.Length-5)}";
				}
				if ( cref == null )
					throw new NotImplementedException();
				typeDesc.Add(new XElement("seealso", new XAttribute("cref", cref)));
			}
		}
		private static void WriteHtmlNodeToXmlElement(HtmlNode hn, XElement xe) {
			try {
				byte[] content;
				using (var ms = new MemoryStream()) {
					using (var sw = new StreamWriter(ms)) {
						sw.Write("<htmlfrag>");
						hn.WriteContentTo(sw);
						sw.Write("</htmlfrag>");
						sw.Flush();
						content = ms.ToArray();
					}
				}
				using (var sr = new StreamReader(new MemoryStream(content, false))) {
					var htmlfrag = XElement.Load(sr);

					var attrsToRemove = (IEnumerable)htmlfrag.XPathEvaluate("//@class|//@id|//@style");
					// ReSharper disable once ConditionIsAlwaysTrueOrFalse
					if (attrsToRemove != null)
						foreach (var attr in attrsToRemove.OfType<XAttribute>())
							attr.Remove();

					// inline code elements become c elements
					ForEachByXPath(htmlfrag, "//code[not(ancestor::pre)]", elem => elem.Name = "c");

					ForEachByXPath(htmlfrag, "//div[/p]", elem => elem.Name = "para");
					
					ForEachByXPath(htmlfrag, "//ul", elem => {
						elem.Name = "list";
						elem.SetAttributeValue("type","bullet");
					});

					ForEachByXPath(htmlfrag, "//ol", elem => {
						elem.Name = "list";
						elem.SetAttributeValue("type","number");
					});

					ForEachByXPath(htmlfrag, "//li", elem => {
						elem.Name = "description";
						elem.ReplaceWith(new XElement("item", elem));
					});
					ForEachByXPath(htmlfrag, "//table/caption", elem => {
						elem.Name = "para";
						elem.RemoveAttributes();
						var table = elem.Parent;
						if (table == null) {
							// logically this should never happen
							throw new NotImplementedException();
						}
						elem.Remove();
						table.AddBeforeSelf(elem);
						
					});

					ForEachByXPath(htmlfrag, "//table/thead/th|//table/tbody/tr/td", elem => {
						elem.Name = "term";
						elem.RemoveAttributes();
					});
					ForEachByXPath(htmlfrag, "//table/tbody/tr", elem => {
						elem.Name = "item";
						elem.RemoveAttributes();
					});
					ForEachByXPath(htmlfrag, "//table/thead", elem => {
						elem.Name = "listheader";
						elem.RemoveAttributes();
					});
					ForEachByXPath(htmlfrag, "//table", elem => {
						elem.Name = "list";
						elem.RemoveAttributes();
						elem.SetAttributeValue("type","table");
					});

					// code elements inside pre elements are stripped
					ForEachByXPath(htmlfrag, "//pre/code", RemoveKeepingDescendants);

					// pre elements are converted into code elements
					ForEachByXPath(htmlfrag, "//pre", elem => elem.Name = "code");

					// anchors are converted to see elements or replaced with full uri 
					ForEachByXPath(htmlfrag, "//a", anchor => {
						var hrefAttr = anchor.Attribute("href");
						if (hrefAttr == null) {
							// likely a target anchor with id removed
							anchor.Remove();
							return;
						}
						var href = hrefAttr.Value;
						if (href.StartsWith(".")) {
							// translate relative uris
							var newHref = new Uri(VkManBasePathUri, href).AbsoluteUri;
							if (newHref.StartsWith("."))
								throw new NotImplementedException();
							hrefAttr.Value = newHref;
							return;
						}
						if (href.StartsWith("#")) {
							// translate relative uris
							RemoveKeepingDescendants(anchor);
							return;
						}
						if (href.StartsWith("http://") || href.StartsWith("https://")) {
							// passthrough absolute uris
							return;
						}
						if (href.StartsWith("/")) {
							throw new NotImplementedException();
						}
						if (!href.EndsWith(".html")) {
							throw new NotImplementedException();
						}
						var cref = href.Substring(0, href.Length - 5);
						anchor.Name = "see";
						anchor.RemoveAttributes();
						if (anchor.Value == cref)
							anchor.RemoveNodes();
						anchor.SetAttributeValue("cref", cref);
					});

					StripByXPath(htmlfrag, "//div|//p|//span|//em|//strong|//col|//colgroup|tbody");

					xe.Add(htmlfrag.Nodes().Cast<object>().ToArray());
				}
			}
			catch (XmlException) {
				// ?!
			}
	
		}

		private static readonly XNode SpaceTextNode = new XText(" ");
		private static readonly IEnumerable<XNode> SpaceTextNodeCombiner = new[] {SpaceTextNode};
		private static InteropAssemblyBuilder _asmBuilder;

		private static void StripByXPath(XNode xe, string xpath) {
			ForEachByXPath(xe, xpath, RemoveKeepingDescendants);
		}

		private static void ForEachByXPath(XNode xe, string xpath, Action<XElement> action) {
			var elems = xe.XPathSelectElements(xpath)
				.OrderByDescending(elem => elem.Ancestors().Count())
				.ToArray();
			int i = 0;
			do {
				foreach (var elem in elems)
					action(elem);
				++i;
				elems = xe.XPathSelectElements(xpath)
					.OrderByDescending(elem => elem.Ancestors().Count())
					.ToArray();
			} while (elems.Any() && i < 5);
		}
			

		private static void RemoveKeepingDescendants(XElement elem) {
			elem.RemoveAttributes();
			for(;;) {
				if (elem.Parent == null)
					break;
				try {
					var descendants = SpaceTextNodeCombiner.Concat(elem.Nodes()).Concat(SpaceTextNodeCombiner);
					if (descendants.Any())
						elem.ReplaceWith(descendants);
					else
						elem.Remove();
				}
				catch (InvalidOperationException) {
					if (elem.Parent == null)
						break;
					continue;
				}
				break;
			}
		}
	}
}