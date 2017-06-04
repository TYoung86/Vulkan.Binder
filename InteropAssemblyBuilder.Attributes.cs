using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

		private static readonly MethodAttributes PropertyMethodAttributes
			= MethodAttributes.Public
			| MethodAttributes.SpecialName
			| MethodAttributes.HideBySig;

		private static readonly MethodAttributes InterfaceMethodAttributes
			= PropertyMethodAttributes
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

		private static readonly AttributeInfo NonVersionableAttributeInfo
			= NonVersionableAttributeCtorInfo != null
			? new AttributeInfo( NonVersionableAttributeCtorInfo )
			: null;

		private static readonly AttributeInfo MethodImplAggressiveInliningAttributeInfo
			= AttributeInfo.Create(() => new MethodImplAttribute(MethodImplOptions.AggressiveInlining));

		private static readonly AttributeInfo StructLayoutSequentialAttributeInfo
			= AttributeInfo.Create(() => new StructLayoutAttribute(LayoutKind.Sequential));

		private static readonly AttributeInfo StructLayoutExplicitAttributeInfo
			= AttributeInfo.Create(() => new StructLayoutAttribute(LayoutKind.Explicit));

		private static readonly AttributeInfo FlagsAttributeInfo
			= AttributeInfo.Create(() => new FlagsAttribute());

		private static void SetMethodInliningAttributes(MethodBuilder method) {
			if ( NonVersionableAttributeInfo != null )
				method.SetCustomAttribute(NonVersionableAttributeInfo);
			method.SetCustomAttribute(MethodImplAggressiveInliningAttributeInfo);
		}
	}
}