using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Xunit;

namespace VulkanTests
{
    public class UnitTest1
    {
        public void Test1() {
	        var a = typeof(vkCreateInstance);
	        var x = VkCullModeFlags.VK_CULL_MODE_NONE;
        }

	    [Fact]
	    public unsafe void StaticCallVkGetInstanceProcAddr() {
		    var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
		    var result = Vulkan.vkGetInstanceProcAddr((VkInstance *)(IntPtr)0, (sbyte*)vkCreateInstanceStr );
			Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), result.Value);
	    }
		
	    [DllImport("kernel32", EntryPoint = "LoadLibrary")]
	    private static extern IntPtr Kernel32LoadLibrary(string fileName);

	    [DllImport("kernel32", EntryPoint = "GetProcAddress")]
	    private static extern IntPtr Kernel32GetProcAddress(IntPtr module, string procName);

	    [DllImport("libdl.so", EntryPoint = "dlopen")]
	    private static extern IntPtr LibDLLoadLibrary(string fileName, int flags);

	    [DllImport("libdl.so", EntryPoint = "dlsym")]
	    private static extern IntPtr LibDLGetProcAddress(IntPtr handle, string name);
		
	    private static IntPtr LoadLibrary(string dllFileName, string soFileName)
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Kernel32LoadLibrary(dllFileName)
				: LibDLLoadLibrary(soFileName, 2);

	    private static IntPtr GetProcAddr(IntPtr handle, string name)
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Kernel32GetProcAddress(handle, name)
				: LibDLGetProcAddress(handle, name);


	    [Fact]
	    public unsafe void DynamicCallVkGetInstanceProcAddr() {
		    var pLib = LoadLibrary("vulkan-1", "libvulkan.so");
		    var pProc = GetProcAddr(pLib, "vkGetInstanceProcAddr");
		    var vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<vkGetInstanceProcAddr>(pProc);
		    var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
		    var result = vkGetInstanceProcAddr((VkInstance *)(IntPtr)0, (sbyte*)vkCreateInstanceStr );
		    Marshal.FreeCoTaskMem(vkCreateInstanceStr);
		    Assert.NotStrictEqual(default(IntPtr), result.Value);

	    }

    }
}
