using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Interop;

[SuppressMessage("ReSharper", "CheckNamespace")]
public static unsafe class Vulkan {

	[DllImport("vulkan-1",
		PreserveSig = true,
		CallingConvention = CallingConvention.Winapi,
		CharSet = CharSet.Ansi,
		BestFitMapping = false,
		ThrowOnUnmappableChar = false,
		SetLastError = false)]
	public static extern PFN_vkVoidFunctionUnmanaged vkGetInstanceProcAddr(VkInstance* pInstance, sbyte* szProc);
	
	public static readonly vkCreateInstance vkCreateInstance;
	public static readonly vkEnumerateInstanceLayerProperties vkEnumerateInstanceLayerProperties;
	public static readonly vkEnumerateInstanceExtensionProperties vkEnumerateInstanceExtensionProperties;


	static Vulkan() {
		var svkCreateInstance = new Utf8String(nameof(vkCreateInstance));
		var svkEnumerateInstanceLayerProperties = new Utf8String(nameof(vkEnumerateInstanceLayerProperties));
		var svkEnumerateInstanceExtensionProperties = new Utf8String(nameof(vkEnumerateInstanceExtensionProperties));
		var value = default(IntPtr);
		try {
			value = vkGetInstanceProcAddr(null, svkEnumerateInstanceLayerProperties).Value;
		}
		catch (Exception ex) {
			System.Diagnostics.Debug.WriteLine($"Failed to import {nameof(vkEnumerateInstanceLayerProperties)} due to {ex.ToString()}");
		}
		try {
			var d = Marshal.GetDelegateForFunctionPointer<vkEnumerateInstanceLayerProperties>(value);
			vkEnumerateInstanceLayerProperties = d;
		}
		catch (MarshalDirectiveException mde) {
			System.Diagnostics.Debug.WriteLine($"Failed to link {nameof(vkEnumerateInstanceLayerProperties)} due to {mde.ToString()}");
		}
		try {
			value = vkGetInstanceProcAddr(null, svkEnumerateInstanceExtensionProperties).Value;
		}
		catch (Exception ex) {
			System.Diagnostics.Debug.WriteLine($"Failed to import {nameof(vkEnumerateInstanceLayerProperties)} due to {ex.ToString()}");
		}
		try {
			var d = Marshal.GetDelegateForFunctionPointer<vkEnumerateInstanceExtensionProperties>(value);
			vkEnumerateInstanceExtensionProperties = d;
		}
		catch (MarshalDirectiveException mde) {
			System.Diagnostics.Debug.WriteLine($"Failed to link {nameof(vkEnumerateInstanceExtensionProperties)} due to {mde.ToString()}");
		}
		try {
			value = vkGetInstanceProcAddr(null, svkCreateInstance).Value;
		}
		catch (Exception ex) {
			System.Diagnostics.Debug.WriteLine($"Failed to import {nameof(vkEnumerateInstanceLayerProperties)} due to {ex.ToString()}");
		}
		try {
			var d = Marshal.GetDelegateForFunctionPointer<vkCreateInstance>(value);
			vkCreateInstance = d;
		}
		catch (MarshalDirectiveException mde) {
			System.Diagnostics.Debug.WriteLine($"Failed to link {nameof(vkCreateInstance)} due to {mde.ToString()}");
		}
	}

}