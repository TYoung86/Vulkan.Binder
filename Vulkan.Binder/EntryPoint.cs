using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Vulkan.Binder {
	public static class EntryPoint {
		public static void Main() {
			var typeInfo = typeof(EntryPoint).GetTypeInfo();
			var assembly = typeInfo.Assembly;
			var alc = AssemblyLoadContext.GetLoadContext(assembly);
			var asmName = assembly.GetName();
			alc.SetProfileOptimizationRoot(Directory.GetCurrentDirectory());
			alc.StartProfileOptimization($"{asmName.Name},v{asmName.Version}.pgo");
			BindingGenerator.Run();
		}
	}
}