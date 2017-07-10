using System;
using System.Linq;
using System.Runtime.InteropServices;
using Interop;
using Xunit;

namespace VulkanTests {
	public class VkGetInstanceProcAddrTests {
		[Fact]
		public unsafe void StaticCallVkGetInstanceProcAddr() {
			var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
			var result = Vulkan.vkGetInstanceProcAddr((VkInstance*) (IntPtr) 0, (sbyte*) vkCreateInstanceStr);
			Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), result.Value);
		}

		[Fact]
		public unsafe void DynamicCallVkGetInstanceProcAddr() {
			var pLib = Native.LoadLibrary("vulkan-1.dll", "libvulkan.so", "libMoltenVK.dylib");
			var pProc = Native.GetProcAddr(pLib, "vkGetInstanceProcAddr");
			var vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<vkGetInstanceProcAddr>(pProc);
			var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
			var result = vkGetInstanceProcAddr((VkInstance*) (IntPtr) 0, (sbyte*) vkCreateInstanceStr);
			Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), result.Value);
		}

		[Fact]
		public unsafe void DynamicAndStaticVkGetInstanceProcAddrGetSamePointer() {
			var procNames = new[] {
				"vkGetInstanceProcAddr",
				"vkEnumerateInstanceLayerProperties",
				"vkEnumerateInstanceExtensionProperties"
			}.Select(Marshal.StringToCoTaskMemUTF8)
			.ToArray();
			try {
				var pLib = Native.LoadLibrary("vulkan-1", "libvulkan.so", "libMoltenVK.dylib");
				var pProc = Native.GetProcAddr(pLib, "vkGetInstanceProcAddr");
				var vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<vkGetInstanceProcAddr>(pProc);
				
				var nullVkInstance = default(VkInstance*);
				foreach (var procName in procNames) {
					var resultStatic = Vulkan.vkGetInstanceProcAddr
						(nullVkInstance, (sbyte*) procName);
					var resultDynamic = vkGetInstanceProcAddr
						(nullVkInstance, (sbyte*) procName);

					Assert.StrictEqual(resultStatic.Value, resultDynamic.Value);
				}
			}
			finally {
				foreach ( var procName in procNames )
					Marshal.FreeCoTaskMem(procName);
			}
		}

		
		[Fact]
		public unsafe void Utf8StringVkGetInstanceProcAddr() {
			var procNames = new[] {
					"vkGetInstanceProcAddr",
					"vkEnumerateInstanceLayerProperties",
					"vkEnumerateInstanceExtensionProperties"
				}.Select(s => new Utf8String(s))
				.ToArray();
			
			var nullVkInstance = default(VkInstance*);
			foreach (var procName in procNames) {
				var resultStatic = Vulkan.vkGetInstanceProcAddr
					(nullVkInstance, procName);

				Assert.NotNull(resultStatic.Value);
			}
		}
	}
}