using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public class CecilTypeComparer : IEqualityComparer<TypeReference>, IEqualityComparer<MethodReference>, IEqualityComparer<ParameterDefinition>, IEqualityComparer<GenericParameter>, IEqualityComparer<CustomAttribute> {
		public static readonly CecilTypeComparer Instance = new CecilTypeComparer();

		// TypeReference

		public static bool IsEqual(TypeReference x, TypeReference y)
			=> x.Is(y);

		bool IEqualityComparer<TypeReference>.Equals(TypeReference x, TypeReference y)
			=> IsEqual(x, y);

		public int GetHashCode(TypeReference obj)
			=> obj.GetHashCode();

		// Method Definition

		public static bool IsEqual(MethodReference x, MethodReference y) {
			var match = x.Name == y.Name
						&& x.ReturnType.Is(y.ReturnType)
						&& x.IsGenericInstance == y.IsGenericInstance
						&& x.Parameters.SequenceEqual(y.Parameters, Instance);
			if (!match) return false;
			if (x.IsGenericInstance)
				match = x.GenericParameters
					.SequenceEqual(y.GenericParameters, Instance);
			return match;
		}

		bool IEqualityComparer<MethodReference>.Equals(MethodReference x, MethodReference y)
			=> IsEqual(x, y);

		public int GetHashCode(MethodReference obj)
			=> obj.GetHashCode();

		// ParameterDefinition

		public static bool IsEqual(ParameterDefinition x, ParameterDefinition y)
			=> x.Attributes == y.Attributes
				&& x.CustomAttributes.SequenceEqual(y.CustomAttributes, Instance)
				&& x.ParameterType.Is(y.ParameterType);

		bool IEqualityComparer<ParameterDefinition>.Equals(ParameterDefinition x, ParameterDefinition y)
			=> IsEqual(x, y);

		public int GetHashCode(ParameterDefinition obj)
			=> obj.GetHashCode();

		// GenericParameter

		public static bool IsEqual(GenericParameter x, GenericParameter y)
			=> x.Type == y.Type
				&& x.Attributes == y.Attributes
				&& x.Constraints.SequenceEqual(y.Constraints, Instance)
				&& x.CustomAttributes.SequenceEqual(y.CustomAttributes, Instance)
				&& x.Is(y);

		bool IEqualityComparer<GenericParameter>.Equals(GenericParameter x, GenericParameter y)
			=> IsEqual(x, y);

		public int GetHashCode(GenericParameter obj)
			=> obj.GetHashCode();

		// CustomAttribute

		public static bool IsEqual(CustomAttribute x, CustomAttribute y)
			=> x.AttributeType.Is(y.AttributeType)
				&& x.GetBlob().SequenceEqual(y.GetBlob());

		bool IEqualityComparer<CustomAttribute>.Equals(CustomAttribute x, CustomAttribute y)
			=> IsEqual(x, y);

		public int GetHashCode(CustomAttribute obj)
			=> obj.GetHashCode();

		public static bool IsEqual(MethodInfo x, MethodReference y) {
			var module = y.Module;
			var match = x.Name == y.Name
						&& x.ReturnType.Import(module).Is(y.ReturnType)
						&& x.IsGenericMethod == y.IsGenericInstance
						&& x.GetParameters().Select(p => p.ParameterType.Import(module))
							.SequenceEqual(y.Parameters.Select(p => p.ParameterType), Instance);
			if (!match) return false;
			if (x.IsGenericMethod)
				match = x.GetGenericArguments().Select(a => a.Import(module))
					.SequenceEqual(y.GenericParameters, Instance);
			return match;
		}

		public static bool IsEqual(ConstructorInfo x, MethodReference y) {
			var module = y.Module;
			var match = x.Name == y.Name
						&& y.ReturnType.Is(y.Module.TypeSystem.Void)
						&& x.IsGenericMethod == y.IsGenericInstance
						&& x.GetParameters().Select(p => p.ParameterType)
							.SequenceEqual(y.Parameters.Select(p => p.ParameterType.GetRuntimeType()));
			if (!match) return false;
			if (x.IsGenericMethod)
				match = x.GetGenericArguments().Select(a => a.Import(module))
					.SequenceEqual(y.GenericParameters, Instance);
			return match;
		}
	}
}