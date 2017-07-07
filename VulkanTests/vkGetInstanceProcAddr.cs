using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Xunit;

namespace VulkanTests {
	public class vkGetInstanceProcAddr {
		[Fact]
		public unsafe void StaticCallVkGetInstanceProcAddr() {
			var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
			var result = Vulkan.vkGetInstanceProcAddr((VkInstance*) (IntPtr) 0, (sbyte*) vkCreateInstanceStr);
			Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), result.Value);
		}

		[Fact]
		public unsafe void DynamicCallVkGetInstanceProcAddr() {
			var pLib = Native.LoadLibrary("vulkan-1", "libvulkan.so");
			var pProc = Native.GetProcAddr(pLib, "vkGetInstanceProcAddr");
			var vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<global::vkGetInstanceProcAddr>(pProc);
			var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
			var result = vkGetInstanceProcAddr((VkInstance*) (IntPtr) 0, (sbyte*) vkCreateInstanceStr);
			Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), result.Value);
		}
	}
}