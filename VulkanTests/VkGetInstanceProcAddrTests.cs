using System;
using System.Linq;
using System.Runtime.InteropServices;
using Interop;
using Xunit;

namespace VulkanTests {
	public class VkGetInstanceProcAddrTests {

		[Fact]
		public unsafe void Utf8StringTest() {
			var procNames = new[] {
				"vkCreateInstance",
				"vkEnumerateInstanceLayerProperties",
				"vkEnumerateInstanceExtensionProperties"
			};
			var procNamesUtf8 = procNames
				.Select(s => new Utf8String(s))
				.ToArray();
			var procNamesAlloc = procNames
				.Select(Marshal.StringToCoTaskMemUTF8)
				.ToArray();

			try {
				for (var i = 0 ; i < procNamesUtf8.Length ; ++i) {
					var procName = procNames[i];
					var procNameUtf8 = procNamesUtf8[i];
					Assert.StrictEqual(procName.Length, (int) procNameUtf8.ByteLength);
					Assert.StrictEqual(procName.Length, (int) procNameUtf8.CharCount);
					var procNameAlloc = (sbyte*) procNamesAlloc[i];
					for (var c = 0 ; c < procNameUtf8.ByteLength ; ++c) {
						Assert.StrictEqual((sbyte) procName[c], procNameUtf8.SBytes[c]);
						Assert.StrictEqual(procNameAlloc[c], procNameUtf8.SBytes[c]);
					}

					var nullTerminatorIndex = procNameUtf8.ByteLength;
					Assert.StrictEqual((sbyte) 0, procNameAlloc[nullTerminatorIndex]);
					Assert.StrictEqual((sbyte) 0, procNameUtf8.Pointer[nullTerminatorIndex]);
				}
			}
			finally {
				foreach (var procName in procNamesAlloc)
					Marshal.FreeCoTaskMem(procName);
			}
		}

		[Fact]
		public unsafe void StaticCallVkGetInstanceProcAddr() {
			var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
			var result = Vulkan.vkGetInstanceProcAddr((VkInstance*) default(IntPtr), (sbyte*) vkCreateInstanceStr);
			Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			Assert.NotStrictEqual(default(IntPtr), (IntPtr) result);
		}
		
		[Fact]
		public unsafe void DynamicCallVkGetInstanceProcAddr() {
			var pLib = Native.LoadLibrary("vulkan-1.dll", "libvulkan.so", "libMoltenVK.dylib");
			var pProc = Native.GetProcAddr(pLib, "vkGetInstanceProcAddr");
			var vkCreateInstanceStr = Marshal.StringToCoTaskMemUTF8("vkCreateInstance");
			try {
				var vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<vkGetInstanceProcAddr>(pProc);
				var result = vkGetInstanceProcAddr((VkInstance*) default(IntPtr), (sbyte*) vkCreateInstanceStr);
				Assert.NotStrictEqual(default(IntPtr), (IntPtr) result);
			}
			finally {
				Marshal.FreeCoTaskMem(vkCreateInstanceStr);
			}
		}

		[Fact]
		public unsafe void DynamicAndStaticVkGetInstanceProcAddrGetSamePointer() {
			var procNames = new[] {
					"vkCreateInstance",
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

					Assert.StrictEqual(resultStatic, resultDynamic);
				}
			}
			finally {
				foreach (var procName in procNames)
					Marshal.FreeCoTaskMem(procName);
			}
		}

		[Fact]
		public unsafe void Utf8StringStaticVkGetInstanceProcAddr() {
			var procNames = new[] {
					"vkCreateInstance",
					"vkEnumerateInstanceLayerProperties",
					"vkEnumerateInstanceExtensionProperties"
				}.Select(s => new Utf8String(s))
				.ToArray();

			var nullVkInstance = (VkInstance*) default(IntPtr);
			foreach (var procName in procNames) {
				var resultStatic = Vulkan.vkGetInstanceProcAddr
					(nullVkInstance, procName.Pointer);

				Assert.NotStrictEqual(default(IntPtr), (IntPtr) resultStatic);
			}
		}

		[Fact]
		public unsafe void Utf8StringDynamicVkGetInstanceProcAddr() {
			var procNames = new[] {
					"vkCreateInstance",
					"vkEnumerateInstanceLayerProperties",
					"vkEnumerateInstanceExtensionProperties"
				}.Select(s => new Utf8String(s))
				.ToArray();


			var nullVkInstance = default(VkInstance*);
			foreach (var procName in procNames) {
				var pLib = Native.LoadLibrary("vulkan-1.dll", "libvulkan.so", "libMoltenVK.dylib");
				var pProc = Native.GetProcAddr(pLib, "vkGetInstanceProcAddr");
				var vkGetInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<vkGetInstanceProcAddr>(pProc);
				var result = vkGetInstanceProcAddr(nullVkInstance, procName);
				Assert.NotStrictEqual(default(IntPtr), (IntPtr) result);
			}
		}

		[Fact]
		public void AutomaticLinkage() {
			
			Assert.NotNull(Vulkan.vkCreateInstance);
			Assert.NotNull(Vulkan.vkEnumerateInstanceExtensionProperties);
			Assert.NotNull(Vulkan.vkEnumerateInstanceLayerProperties);
		}

	}
}