using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private void IntegrateInteropTypes(IEnumerable<TypeDefinition> tds) {
			foreach (var td in tds) {
				UpdateMethodInliningAttributes(td);
				foreach (var nt in td.NestedTypes) {
					UpdateMethodInliningAttributes(nt);
				}
			}
		}

		private void UpdateMethodInliningAttributes(TypeDefinition td) {
			var tdMethods = td.Methods
				.Union(td.Properties.SelectMany
					(props => new[] {props.GetMethod, props.SetMethod}))
					.Where(md => md != null);
			foreach (var md in tdMethods) {
				var attrs = md.CustomAttributes;
				if (NonVersionableAttribute != null) {
					var nv = NonVersionableAttribute.AttributeType;
					if (attrs.All(ca => !ca.AttributeType.Is(nv)))
						attrs.Add(NonVersionableAttribute);
				}
				var mi = MethodImplAggressiveInliningAttribute.AttributeType;
				if (attrs.All(ca => !ca.AttributeType.Is(mi)))
					attrs.Add(MethodImplAggressiveInliningAttribute);
			}
		}
	}
}