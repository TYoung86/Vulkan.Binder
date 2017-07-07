using System;
using System.Runtime.InteropServices;

namespace VulkanTests {
	public static class Native {
		
		[DllImport("kernel32", EntryPoint = "LoadLibrary")]
		private static extern IntPtr Kernel32LoadLibrary(string fileName);

		[DllImport("kernel32", EntryPoint = "GetProcAddress")]
		private static extern IntPtr Kernel32GetProcAddress(IntPtr module, string procName);

		[DllImport("dl", EntryPoint = "dlopen")]
		private static extern IntPtr LibDLLoadLibrary(string fileName, int flags);

		[DllImport("dl", EntryPoint = "dlsym")]
		private static extern IntPtr LibDLGetProcAddress(IntPtr handle, string name);
		
		public static IntPtr LoadLibrary(string dllFileName, string soFileName)
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Kernel32LoadLibrary(dllFileName)
				: LibDLLoadLibrary(soFileName, 2);

		public static IntPtr GetProcAddr(IntPtr handle, string name)
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Kernel32GetProcAddress(handle, name)
				: LibDLGetProcAddress(handle, name);

	}
}