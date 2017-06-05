using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Artilect.Vulkan.Binder.Extensions {
	public static class TypeExtensions {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MarshalSizeOf(this Type type) => Marshal.SizeOf(type);

		private static readonly MethodInfo UnsafeSizeOfGmd =
			((MethodCallExpression) ((Expression<Func<int>>)
				(() => Unsafe.SizeOf<int>())).Body).Method
			.GetGenericMethodDefinition();

		private static readonly ConcurrentDictionary<Type, int>
			UnsafeSizeOfTypes = new ConcurrentDictionary<Type, int>();


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SizeOf(this Type type) {
			if (type.IsPointer)
				return IntPtr.Size;
			if (type.IsEnum)
				try {
					var underlyingType = type.UnderlyingSystemType;
					if ( !underlyingType.IsEnum )
						return underlyingType.SizeOf();
				}
				catch { /*...*/ }
			try {
				return (type as TypeBuilder)?.Size
					?? type.MarshalSizeOf();
			}
			catch (ArgumentException) {
				return type.UnsafeSizeOf();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int UnsafeSizeOf(this Type type)
			=> UnsafeSizeOfTypes.TryGetValue(type, out var size)
				? size
				: (UnsafeSizeOfTypes[type] = (int) UnsafeSizeOfGmd.MakeGenericMethod(type).Invoke(null, null));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDirect(this Type type)
			=> !type.IsIndirect();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsIndirect(this Type type)
			=> type.IsArray || type.IsPointer || type.IsByRef;
	}
}