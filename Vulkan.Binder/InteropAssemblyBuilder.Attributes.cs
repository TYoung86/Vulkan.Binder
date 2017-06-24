using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
//using System.Reflection;
//using System.Reflection.Emit;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private const TypeAttributes PublicSealedTypeAttributes
			= TypeAttributes.Public
			| TypeAttributes.Sealed;
		
		private const TypeAttributes PublicSealedStructTypeAttributes
			= PublicSealedTypeAttributes
			| TypeAttributes.SequentialLayout;
		
		private const TypeAttributes PublicSealedUnionTypeAttributes
			= PublicSealedTypeAttributes
			| TypeAttributes.ExplicitLayout;

		private const TypeAttributes DelegateTypeAttributes
			= PublicSealedTypeAttributes
			| TypeAttributes.AutoClass;

		private const TypeAttributes PublicInterfaceTypeAttributes
			= TypeAttributes.Public
			| TypeAttributes.Abstract
			| TypeAttributes.Interface;

		private const MethodAttributes PublicInterfaceImplementationMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.Virtual
			| MethodAttributes.HideBySig;

		private const MethodAttributes PublicStaticMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.Static
			| MethodAttributes.HideBySig;

		private const MethodAttributes HiddenPropertyMethodAttributes
			= MethodAttributes.Private // PrivateScope
			| MethodAttributes.SpecialName
			| MethodAttributes.HideBySig;

		private const MethodAttributes InterfaceMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.SpecialName
			| MethodAttributes.HideBySig
			| MethodAttributes.Abstract
			| MethodAttributes.Virtual;

		private const MethodAttributes DelegateInvokeMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.HideBySig
			| MethodAttributes.Virtual;

		private const MethodAttributes DelegateConstructorAttributes
			= MethodAttributes.Public
			| MethodAttributes.RTSpecialName
			| MethodAttributes.HideBySig;

		private static readonly Type NonVersionableAttributeType
			= Type.GetType("System.Runtime.Versioning.NonVersionableAttribute", false);

		private static readonly ConstructorInfo NonVersionableAttributeCtorInfo
			= NonVersionableAttributeType?.GetConstructors().FirstOrDefault();

		private readonly CustomAttribute NonVersionableAttribute;

		private static readonly AttributeInfo NonVersionableAttributeInfo
			= NonVersionableAttributeCtorInfo != null
			? new AttributeInfo( NonVersionableAttributeCtorInfo )
			: null;

		private readonly CustomAttribute MethodImplAggressiveInliningAttribute;

		private static readonly AttributeInfo MethodImplAggressiveInliningAttributeInfo
			= AttributeInfo.Create(() => new MethodImplAttribute(MethodImplOptions.AggressiveInlining));

		//private static readonly AttributeInfo StructLayoutSequentialAttributeInfo
		//	= AttributeInfo.Create(() => new StructLayoutAttribute(LayoutKind.Sequential));

		//private static readonly AttributeInfo StructLayoutExplicitAttributeInfo
		//	= AttributeInfo.Create(() => new StructLayoutAttribute(LayoutKind.Explicit));

		private readonly CustomAttribute FlagsAttribute;

		private static readonly AttributeInfo FlagsAttributeInfo
			= AttributeInfo.Create(() => new FlagsAttribute());

		private void SetMethodInliningAttributes(MethodDefinition method) {
			if ( NonVersionableAttributeInfo != null )
				method.CustomAttributes.Add(NonVersionableAttribute);
			method.CustomAttributes.Add(MethodImplAggressiveInliningAttribute);
		}
	}
}