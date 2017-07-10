using System;
using System.Runtime.InteropServices;

namespace VulkanTests {
	public static class Native {
		private const string WindowsKernel32 = "kernel32";
		private const string LinuxBsdLibDl = "dl";
		private const string MacOsXLibSystem = "/usr/lib/libSystem.dylib";

		[DllImport(WindowsKernel32, EntryPoint = "LoadLibrary")]
		private static extern IntPtr Kernel32LoadLibrary(string fileName);

		[DllImport(WindowsKernel32, EntryPoint = "GetProcAddress")]
		private static extern IntPtr Kernel32GetProcAddress(IntPtr module, string procName);

		[DllImport(LinuxBsdLibDl, EntryPoint = "dlopen")]
		private static extern IntPtr LibDL_DLOpen(string fileName, int flags);

		[DllImport(LinuxBsdLibDl, EntryPoint = "dlsym")]
		private static extern IntPtr LibDL_DLSym(IntPtr handle, string name);

		[DllImport(MacOsXLibSystem, EntryPoint = "dlopen")]
		private static extern IntPtr LibSystem_DLOpen(string fileName, int flags);

		[DllImport(MacOsXLibSystem, EntryPoint = "dlsym")]
		private static extern IntPtr LibSystem_DLSym(IntPtr handle, string name);
		
		public static IntPtr LoadLibrary(string dllFileName, string soFileName, string dylibFileName)
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Kernel32LoadLibrary(dllFileName)
				: RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
					? LibSystem_DLOpen(dylibFileName, 2)
					: LibDL_DLOpen(soFileName, 2);

		public static IntPtr GetProcAddr(IntPtr handle, string name)
			=> RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? Kernel32GetProcAddress(handle, name)
				: RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
					? LibSystem_DLSym(handle, name)
					:  LibDL_DLSym(handle, name);

	}
}