//
// NOTES
// =====
// This project is a simple shell.  The folder structure in the Solution Explorer is a suggested folder
// layout that mimics the existing presentation styles in Sandcastle.  If this assembly will contain
// multiple presentation styles, group the presentation style folders into a subfolder named after the
// presentation style.
//
// Copy the files from an existing presentation style to get a head start or start from scratch by creating
// your own.  When adding files to the folders, be sure to set their Build Action property to "Content" and
// their Copy to Output Folder property to "Copy if newer".
//

using System.Collections.Generic;
using System.Reflection;
using Sandcastle.Core;
using Sandcastle.Core.PresentationStyle;

namespace Vulkan.Sandcastle.Presentation {
	/// <summary>
	/// TODO: Set your presentation style's unique ID and description in the export attribute below.
	/// </summary>
	/// <remarks>The <c>PresentationStyleExportAttribute</c> is used to export your presentation style so
	/// that the help file builder finds it and can make use of it.  The example below shows the basic usage
	/// for a common presentation style.  Presentation styles are singletons in nature.  The composition
	/// container will create instances as needed and will dispose of them when the container is disposed
	/// of.</remarks>
	[PresentationStyleExport("VulkanSandcastlePresentation",
		"Vulkan.Sandcastle.Presentation",
		Version = AssemblyInfo.ProductVersion,
		Copyright = AssemblyInfo.Copyright,
		Description = "Vulkan.Sandcastle.Presentation custom presentation style")]
	public sealed class VulkanSandcastlePresentationStyle : PresentationStyleSettings {

		/// <inheritdoc />
		public override string Location
			=> ComponentUtilities.AssemblyFolder(Assembly.GetExecutingAssembly());

		/// <summary>
		/// Constructor
		/// </summary>
		public VulkanSandcastlePresentationStyle() {
			// The base path of the presentation style files relative to the assembly's location.  If your
			// assembly will reside in the same folder as the presentation style content, you can remove this
			// property setting.  If adding multiple presentation styles to the assembly, set this to the name
			// of the subfolder that contains the presentation style content folders.
			BasePath = "VulkanSandcastlePresentation";

			// TODO: Adjust the rest of these properties as needed.

			SupportedFormats = HelpFileFormats.Website;


			SupportsNamespaceGrouping = SupportsCodeSnippetGrouping = true;

			// If relative, these paths are relative to the base path
			ResourceItemsPath = "Content";

			ToolResourceItemsPath = "SHFBContent";


			DocumentModelTransformation = new TransformationFile(
				@"%SHFBROOT%\ProductionTransforms\ApplyVSDocModel.xsl", new Dictionary<string, string> {
					{"IncludeAllMembersTopic", "false"},
					{"project", "{@ProjectNodeIDOptional}"}
				});


			IntermediateTocTransformation = new TransformationFile(
				@"%SHFBROOT%\ProductionTransforms\CreateVSToc.xsl");


			BuildAssemblerConfiguration = @"Configuration\BuildAssembler.config";

			// Note that UNIX based web servers may be case-sensitive with regard to folder and filenames so
			// match the case of the folder and filenames in the literals to their actual casing on the file
			// system.

			ContentFiles.Add(new ContentFiles(SupportedFormats, @"icons\*.*"));

			ContentFiles.Add(new ContentFiles(SupportedFormats, @"scripts\*.*"));

			ContentFiles.Add(new ContentFiles(SupportedFormats, @"styles\*.*"));

			// By default, this will use the standard web file content from the Sandcastle Help File Builder
			ContentFiles.Add(new ContentFiles(HelpFileFormats.Website, null, @"Web\*.*",
				string.Empty, new[] {".aspx", ".html", ".htm", ".php"}));
			

			TransformComponentArguments.Add(new TransformComponentArgument("logoFile", true, true, null,
				"An optional logo file to insert into the topic headers.  Specify the filename only, omit " +
				"the path.  Place the file in your project in an icons\\ folder and set the Build Action to " +
				"Content.  If blank, no logo will appear in the topic headers.  If building website output " +
				"and your web server is case-sensitive, be sure to match the case of the folder name in your " +
				"project with that of the presentation style.  The same applies to the logo filename itself."));


			TransformComponentArguments.Add(new TransformComponentArgument("logoFileSvg", true, true, null,
				"An optional logo svg file to replace the logo file image.  Specify the filename only, omit " +
				"the path.  Place the file in your project in an icons\\ folder and set the Build Action to " +
				"Content.  If blank, only the non-svg logo will appear in the topic headers.  If building website output " +
				"and your web server is case-sensitive, be sure to match the case of the folder name in your " +
				"project with that of the presentation style.  The same applies to the logo filename itself."));

			TransformComponentArguments.Add(new TransformComponentArgument("logoHeight", true, true, null,
				"An optional logo height.  If left blank, the actual logo image height is used."));

			TransformComponentArguments.Add(new TransformComponentArgument("logoWidth", true, true, null,
				"An optional logo width.  If left blank, the actual logo image width is used."));

			TransformComponentArguments.Add(new TransformComponentArgument("logoAltText", true, true, null,
				"Optional logo alternate text.  If left blank, no alternate text is added."));

			TransformComponentArguments.Add(new TransformComponentArgument("logoPlacement", true, true,
				"left", "An optional logo placement.  Specify left, right, or above.  If not specified, the " +
						"default is left."));

			TransformComponentArguments.Add(new TransformComponentArgument("logoAlignment", true, true,
				"left", "An optional logo alignment when using the 'above' placement option.  Specify left, " +
						"right, or center.  If not specified, the default is left."));

			TransformComponentArguments.Add(new TransformComponentArgument("maxVersionParts", false, true,
				null, "The maximum number of assembly version parts to show in API member topics.  Set to 2, " +
					"3, or 4 to limit it to 2, 3, or 4 parts or leave it blank for all parts including the " +
					"assembly file version value if specified."));

			TransformComponentArguments.Add(new TransformComponentArgument("defaultLanguage", true, true,
				"cs", "The default language to use for syntax sections, code snippets, and a language-specific " +
					"text.  This should be set to cs, vb, cpp, fs, or the keyword style parameter value of a " +
					"third-party syntax generator if you want to use a non-standard language as the default."));

			TransformComponentArguments.Add(new TransformComponentArgument("includeEnumValues", false, true,
				"true", "Set this to 'true' to include the column for the numeric value of each field in " +
						"enumerated type topics.  Set it to 'false' to omit the numeric values column."));

			TransformComponentArguments.Add(new TransformComponentArgument("baseSourceCodeUrl", false, true,
				null, "If you set the Source Code Base Path property in the Paths category, specify the URL to " +
					"the base source code folder on your project's website here.  Some examples for GitHub are " +
					"shown below.\r\n\r\n" +
					"Important: Be sure to set the Source Code Base Path property and terminate the URL below with " +
					"a slash if necessary.\r\n\r\n" +
					"Format: https://github.com/YourUserID/YourProject/blob/BranchNameOrCommitHash/BaseSourcePath/ \r\n\r\n" +
					"Master branch: https://github.com/JohnDoe/WidgestProject/blob/master/src/ \r\n" +
					"A different branch: https://github.com/JohnDoe/WidgestProject/blob/dev-branch/src/ \r\n" +
					"A specific commit: https://github.com/JohnDoe/WidgestProject/blob/c6e41c4fc2a4a335352d2ae8e7e85a1859751662/src/"));

			TransformComponentArguments.Add(new TransformComponentArgument("requestExampleUrl", false, true,
				null, "To include a link that allows users to request an example for an API topic, set the URL " +
					"to which the request will be sent.  This can be a web page URL or an e-mail URL.  Only include " +
					"the URL as the parameters will be added automatically by the topic.  For example:\r\n\r\n" +
					"Create a new issue on GitHub: https://github.com/YourUserID/YourProject/issues/new \r\n" +
					"Send via e-mail: mailto:YourEmailAddress@Domain.com"));

			// Add plug-in dependencies
			PlugInDependencies.Add(new PlugInDependency("Lightweight Website Style", null));
		}

	}
}