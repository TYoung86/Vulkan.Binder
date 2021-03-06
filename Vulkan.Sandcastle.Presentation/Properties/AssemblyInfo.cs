using System;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;

// General assembly information
[assembly: AssemblyProduct("VulkanSandcastlePresentation")]
[assembly: AssemblyTitle("Vulkan.Sandcastle.Presentation")]
[assembly: AssemblyDescription("A custom presentation style for Sandcastle")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright(AssemblyInfo.Copyright)]
[assembly: AssemblyCulture("")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]

[assembly: CLSCompliant(true)]

// Resources contained within the assembly are English
[assembly: NeutralResourcesLanguage("en")]

[assembly: AssemblyVersion(AssemblyInfo.ProductVersion)]
[assembly: AssemblyFileVersion(AssemblyInfo.ProductVersion)]
[assembly: AssemblyInformationalVersion(AssemblyInfo.ProductVersion)]

// This defines constants that can be used here and in the custom presentation style export attribute
// ReSharper disable once CheckNamespace
internal static class AssemblyInfo {

	// Product version
	public const string ProductVersion = "1.0.0.0";

	// Assembly copyright information
	public const string Copyright = "Copyright \xA9 2017 Tyler Young, All Rights Reserved.";

}