using System.Collections.Generic;
using System.Reflection;

namespace Artilect.Vulkan.Binder {
	public sealed class CustomCustomAttributeData : CustomAttributeData {
		public CustomCustomAttributeData(ConstructorInfo constructor, IList<CustomAttributeTypedArgument> constructorArguments, IList<CustomAttributeNamedArgument> namedArguments) {
			Constructor = constructor;
			ConstructorArguments = constructorArguments;
			NamedArguments = namedArguments;
		}

		public override ConstructorInfo Constructor { get; }

		public override IList<CustomAttributeTypedArgument> ConstructorArguments { get; }

		public override IList<CustomAttributeNamedArgument> NamedArguments { get; }
	}
}