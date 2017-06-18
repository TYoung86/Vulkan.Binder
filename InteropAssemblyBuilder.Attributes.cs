using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
//using System.Reflection;
//using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Artilect.Vulkan.Binder.Extensions;
using Mono.Cecil;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private static readonly TypeAttributes PublicSealedTypeAttributes
			= TypeAttributes.Public
			| TypeAttributes.Sealed;
		
		private static readonly TypeAttributes PublicSealedStructTypeAttributes
			= PublicSealedTypeAttributes
			| TypeAttributes.SequentialLayout;
		
		private static readonly TypeAttributes PublicSealedUnionTypeAttributes
			= PublicSealedTypeAttributes
			| TypeAttributes.ExplicitLayout;

		private static readonly TypeAttributes DelegateTypeAttributes
			= PublicSealedTypeAttributes
			| TypeAttributes.AutoClass;

		private static readonly TypeAttributes PublicInterfaceTypeAttributes
			= TypeAttributes.Public
			| TypeAttributes.Abstract
			| TypeAttributes.Interface;

		private static readonly MethodAttributes PublicInterfaceImplementationMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.Virtual
			| MethodAttributes.HideBySig;
		
		private static readonly MethodAttributes PublicStaticMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.Static
			| MethodAttributes.HideBySig;
		
		private static readonly MethodAttributes PublicPropertyMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.SpecialName
			| MethodAttributes.HideBySig;

		private static readonly MethodAttributes HiddenPropertyMethodAttributes
			= MethodAttributes.Private // PrivateScope
			| MethodAttributes.SpecialName
			| MethodAttributes.HideBySig;

		private static readonly MethodAttributes InterfaceMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.SpecialName
			| MethodAttributes.HideBySig
			| MethodAttributes.Abstract
			| MethodAttributes.Virtual;

		private static readonly MethodAttributes DelegateInvokeMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.HideBySig
			| MethodAttributes.Virtual;

		private static readonly MethodAttributes DelegateConstructorAttributes
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

		private static readonly AttributeInfo FlagsAttributeInfo
			= AttributeInfo.Create(() => new FlagsAttribute());

		private void SetMethodInliningAttributes(MethodDefinition method) {
			if ( NonVersionableAttributeInfo != null )
				method.CustomAttributes.Add(NonVersionableAttribute);
			method.CustomAttributes.Add(MethodImplAggressiveInliningAttribute);
		}
	}
}