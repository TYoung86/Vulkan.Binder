using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ClangSharp;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {

		private static readonly ImmutableHashSet<CXTypeKind> SignedCxTypeKinds = (new[] {
			CXTypeKind.CXType_Char_S, CXTypeKind.CXType_SChar, CXTypeKind.CXType_WChar,
			CXTypeKind.CXType_Float, CXTypeKind.CXType_Double, CXTypeKind.CXType_LongDouble,
			CXTypeKind.CXType_Short, CXTypeKind.CXType_Int, CXTypeKind.CXType_Long, CXTypeKind.CXType_LongLong
		}).ToImmutableHashSet();

		private static ImmutableDictionary<CXTypeKind, Type> PrimitiveTypeMap {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new Dictionary<CXTypeKind, Type> {
			{CXTypeKind.CXType_Char_S, typeof(sbyte)},
			{CXTypeKind.CXType_Char_U, typeof(byte)},
			{CXTypeKind.CXType_SChar, typeof(sbyte)},
			{CXTypeKind.CXType_UChar, typeof(byte)},
			{CXTypeKind.CXType_Int, typeof(int)},
			{CXTypeKind.CXType_UInt, typeof(uint)},
			{CXTypeKind.CXType_Short, typeof(short)},
			{CXTypeKind.CXType_UShort, typeof(ushort)},
			{CXTypeKind.CXType_LongLong, typeof(long)},
			{CXTypeKind.CXType_ULongLong, typeof(ulong)},
			{CXTypeKind.CXType_Float, typeof(float)},
			{CXTypeKind.CXType_Double, typeof(double)},
			{CXTypeKind.CXType_LongDouble, typeof(double)},
#if VAR_LONGS
			{CXTypeKind.CXType_Long, typeof(IntPtr)},
			{CXTypeKind.CXType_ULong, typeof(UIntPtr)},
#else
			{CXTypeKind.CXType_Long, typeof(long)},
			{CXTypeKind.CXType_ULong, typeof(ulong)},
#endif
			{CXTypeKind.CXType_Pointer, typeof(IntPtr)},
			{CXTypeKind.CXType_Char16, typeof(char)},
			{CXTypeKind.CXType_WChar, typeof(char)},
			{CXTypeKind.CXType_Char32, typeof(uint)},
			{CXTypeKind.CXType_Bool, typeof(bool)},
			{CXTypeKind.CXType_Void, typeof(void)}
		}.ToImmutableDictionary();

		private static ImmutableDictionary<Type, UnmanagedType> PrimitiveUnmanagedTypeMap {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new Dictionary<Type, UnmanagedType> {
			{typeof(sbyte), UnmanagedType.I1},
			{typeof(byte), UnmanagedType.U1},
			{typeof(int), UnmanagedType.I4},
			{typeof(uint), UnmanagedType.U4},
			{typeof(short), UnmanagedType.I2},
			{typeof(ushort), UnmanagedType.U2},
			{typeof(long), UnmanagedType.I8},
			{typeof(ulong), UnmanagedType.U8},
			{typeof(float), UnmanagedType.R4},
			{typeof(double), UnmanagedType.R8},
			{typeof(char), UnmanagedType.I2},
			{typeof(bool), UnmanagedType.I1},
			{typeof(IntPtr), UnmanagedType.SysInt},
			{typeof(UIntPtr), UnmanagedType.SysUInt}
		}.ToImmutableDictionary();

		private static ImmutableDictionary<CXCallingConv, CallingConvention> CallingConventionMap {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = new Dictionary<CXCallingConv, CallingConvention> {
			{CXCallingConv.CXCallingConv_C, CallingConvention.Cdecl},
			{CXCallingConv.CXCallingConv_X86FastCall, CallingConvention.FastCall},
			{CXCallingConv.CXCallingConv_X86StdCall, CallingConvention.StdCall},
			{CXCallingConv.CXCallingConv_X86ThisCall, CallingConvention.ThisCall}
		}.ToImmutableDictionary();

		private static ImmutableDictionary<CallingConvention, AttributeInfo> ClrCallingConventionAttributeMap {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get;
		} = Enum.GetValues(typeof(CallingConvention)).Cast<CallingConvention>().Select(callConv =>
			new KeyValuePair<CallingConvention, AttributeInfo>(callConv,
				AttributeInfo.Create(() => new UnmanagedFunctionPointerAttribute(callConv)))
		).ToImmutableDictionary();

		private static bool IsCursorInSystemHeader(CXCursor cursor) {
			return clang.Location_isInSystemHeader(clang.getCursorLocation(cursor)) != 0;
		}

		private static CXTypeKind CanonizeType(ref CXType type, out CXCursor typeDeclCursor) {
			//var originalType = type;
			typeDeclCursor = clang.getTypeDeclaration(type);
			/*
			var typeName = type.ToString();
			switch (typeName) {
				case "size_t": {
					return CXTypeKind.CXType_UInt128;
				}
				case "ptrdiff_t": {
					return CXTypeKind.CXType_Int128;
				}
			}
			*/
			if (type.kind == CXTypeKind.CXType_Typedef) {
				if (IsCursorInSystemHeader(typeDeclCursor))
					type = clang.getCanonicalType(type);
				for (; ;) {
					var underlyingType = clang.getTypedefDeclUnderlyingType(typeDeclCursor);
					if (underlyingType.kind != CXTypeKind.CXType_Invalid) {
						type = underlyingType;
						typeDeclCursor = clang.getTypeDeclaration(type);
						continue;
					}
					break;
				}
			}
			
			return type.kind;
		}

		private CustomParameterInfo ResolveField(CXType originalType, string name = null, int index = 0) {
			var type = originalType;

			if (type.kind == CXTypeKind.CXType_FunctionProto)
				throw new NotImplementedException();
			if (type.kind == CXTypeKind.CXType_FunctionNoProto)
				throw new NotImplementedException();
			var typeKind = CanonizeType(ref type, out var typeDeclCursor);

			if (typeKind == CXTypeKind.CXType_Pointer) {
				var pointeeType = clang.getPointeeType(type);
				if (clang.getFunctionTypeCallingConv(pointeeType) != CXCallingConv.CXCallingConv_Invalid) {
					var delegateTypeName = originalType.ToString();
					var possibleDelegateType = Assembly.GetType(delegateTypeName, false, false);

					if (possibleDelegateType != null)
						return new CustomParameterInfo(name, possibleDelegateType) {Position = index};

					return new CustomParameterInfo(name,
						IncompleteType.Get(Assembly, Module, null, delegateTypeName)) {Position = index};
				}

				var resolvedParameter = ResolveField(pointeeType);
				if (resolvedParameter.Attributes != default(ParameterAttributes)
					|| resolvedParameter.CustomAttributes.Any())
					throw new NotImplementedException();
				return new CustomParameterInfo(name,
					resolvedParameter.ParameterType.MakePointerType());
			}
			if (typeKind == CXTypeKind.CXType_ConstantArray) {
				var arraySize = (int) clang.getArraySize(type);
				var elementType = clang.getArrayElementType(type);
				var resolvedParameter = ResolveField(elementType, name);
				if (resolvedParameter.Attributes != default(ParameterAttributes)
					|| resolvedParameter.CustomAttributes.Any())
					throw new NotImplementedException();
				var arrayType = resolvedParameter.ParameterType.MakeArrayType();
				return new CustomParameterInfo(name,
					arrayType, AttributeInfo.Create(() =>
						new FixedBufferAttribute(arrayType, arraySize))) {Position = index};
			}
			if (PrimitiveTypeMap.TryGetValue(typeKind, out var primitiveType)) {
				if (primitiveType == null)
					throw new NotImplementedException();
				return new CustomParameterInfo(name, primitiveType) {Position = index};
			}

			var typeName = typeDeclCursor.ToString();
			var possibleType = Assembly.GetType(typeName, false, false);
			if (possibleType != null)
				return new CustomParameterInfo(name, possibleType) {Position = index};

			return new CustomParameterInfo(name,
				IncompleteType.Get(Assembly, Module, null, typeName)) {Position = index};
		}

		private CustomParameterInfo ResolveParameter(CXType originalType, string name = null, int index = 0) {
			var type = originalType;
			if (type.kind == CXTypeKind.CXType_FunctionProto)
				throw new NotImplementedException();
			if (type.kind == CXTypeKind.CXType_FunctionNoProto)
				throw new NotImplementedException();
			var typeKind = CanonizeType(ref type, out var typeDeclCursor);
			if (typeKind == CXTypeKind.CXType_Pointer) {
				var pointeeType = clang.getPointeeType(type);
				if (clang.getFunctionTypeCallingConv(pointeeType) != CXCallingConv.CXCallingConv_Invalid) {
					var delegateTypeName = originalType.ToString();
					var possibleDelegateType = Assembly.GetType(delegateTypeName, false, false);
					if (possibleDelegateType != null)
						return new CustomParameterInfo(name, possibleDelegateType) {Position = index};

					return new CustomParameterInfo(name,
						IncompleteType.Get(Assembly, Module, null, delegateTypeName)) {Position = index};
				}
				var resolvedParameter = ResolveParameter(pointeeType);
				if (resolvedParameter.Attributes != default(ParameterAttributes)
					|| resolvedParameter.CustomAttributes.Any())
					throw new NotImplementedException();
				return new CustomParameterInfo(name,
					resolvedParameter.ParameterType.MakePointerType()) {Position = index};
			}
			if (typeKind == CXTypeKind.CXType_DependentSizedArray) {
				throw new NotImplementedException();
			}
			if (typeKind == CXTypeKind.CXType_ConstantArray) {
				var arraySize = (int) clang.getArraySize(type);
				var elementType = clang.getArrayElementType(type);
				var resolvedParameter = ResolveParameter(elementType, name);
				if (resolvedParameter.Attributes != default(ParameterAttributes)
					|| resolvedParameter.CustomAttributes.Any())
					throw new NotImplementedException();
				var clrElementType = resolvedParameter.ParameterType;
				if (clrElementType.IsPointer) clrElementType = typeof(IntPtr);
				var arrayType = resolvedParameter.ParameterType.MakeArrayType();

				if (!PrimitiveUnmanagedTypeMap.TryGetValue(clrElementType, out var unmanagedType)) {
					throw new NotImplementedException();
				}

				return new CustomParameterInfo(name,
					arrayType, AttributeInfo.Create(() =>
						new MarshalAsAttribute(UnmanagedType.LPArray) {
							ArraySubType = unmanagedType,
							SizeConst = arraySize
						})) {Position = index};
			}
			if (PrimitiveTypeMap.TryGetValue(typeKind, out var primitiveType)) {
				if (primitiveType == null)
					throw new NotImplementedException();
				return new CustomParameterInfo(name, primitiveType) {Position = index};
			}

			var typeName = typeDeclCursor.ToString();
			var possibleType = Assembly.GetType(typeName, false, false);
			if (possibleType != null)
				return new CustomParameterInfo(name, possibleType) {Position = index};

			return new CustomParameterInfo(name,
				IncompleteType.Get(Assembly, Module, null, typeName)) {Position = index};
		}

		private static bool IsTypeSigned(Type type)
			=> Convert.ToBoolean(type.GetField("MinValue")?.GetValue(null) ?? false);
	}
}