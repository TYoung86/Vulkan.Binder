using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace Vulkan.Binder {
	public class AssemblyNameReferenceVersionComparer : IComparer<AssemblyNameReference> {
		public static readonly AssemblyNameReferenceVersionComparer Instance
			= new AssemblyNameReferenceVersionComparer();
		public int Compare(AssemblyNameReference x, AssemblyNameReference y) {
			if (x == y) return 0;
			if (x == null) return -1;
			if (y == null) return 1;
			var nameCheck = StringComparer.Ordinal.Compare(x.Name, y.Name);
			return nameCheck != 0
				? nameCheck
				: x.Version.CompareTo(y.Version);
		}
	}
}