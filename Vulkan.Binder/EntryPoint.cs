using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Vulkan.Binder {
	public static class EntryPoint {
		public static void Main() {
			var pgo = Type.GetType("System.Runtime.ProfileOptimization", false);
			if (pgo != null) {
				var setRoot = pgo.GetMethod("SetProfileRoot", BindingFlags.Public|BindingFlags.Static);
				var start = pgo.GetMethod("StartProfiling", BindingFlags.Public|BindingFlags.Static);
				setRoot.Invoke(null, new object[] { Directory.GetCurrentDirectory() });
				var asmName = typeof(EntryPoint).GetTypeInfo().Assembly.GetName();
				start.Invoke(null, new object[] {
					string.Concat( asmName.Name,
					",v", asmName.Version.ToString(),
					".pgo" )
				});
			}

			BindingGenerator.Run();
		}
	}
}