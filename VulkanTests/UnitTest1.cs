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
	    public unsafe void CallVkGetInstanceProcAddr() {
			
		    var vkCreateInstanceStr = Marshal.StringToHGlobalAnsi("vkCreateInstance");
		    var result = Vulkan.vkGetInstanceProcAddr((VkInstance *)(IntPtr)0, (sbyte*)vkCreateInstanceStr );
			Marshal.FreeHGlobal(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), result.Value);
	    }

    }
}
