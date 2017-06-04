using System;
using System.Collections.Generic;
using Artilect.Interop;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		public delegate void TypeTransform(ref Type t);

		public static void MakeArrayType(ref Type t) => t = t.MakeArrayType();
		public static void MakeByRefType(ref Type t) => t = t.MakeByRefType();
		public static void MakePointerType(ref Type t) => t = t.MakePointerType();
		
		public static void MakePointer32Type(ref Type t)
			=> t = typeof(Pointer32<>).MakeGenericType(t);

		public static void MakePointer64Type(ref Type t)
			=> t = typeof(Pointer64<>).MakeGenericType(t);

		public static Type GetTypePointedTo(Type exterior, out LinkedList<TypeTransform> transform, bool directVoids = false) {
			var voidPtrType = typeof(void*);
			var type = exterior;
			transform = new LinkedList<TypeTransform>();
			while (type.HasElementType) {
				if (type.IsPointer) {
					if (directVoids || type != voidPtrType) {
						transform.AddFirst(MakePointerType);
						type = type.GetElementType();
					}
					else
						break;
				}
				else
					return type;
			}
			return type;
		}

		public Type GetInteriorType(Type exterior, out LinkedList<TypeTransform> transform, bool directVoids = false) {
			transform = new LinkedList<TypeTransform>();
			return FindInteriorType(exterior, ref transform, directVoids);
		}

		public Type FindInteriorType(Type exterior, ref LinkedList<TypeTransform> transform, bool directVoids = false) {
			var voidPtrType = typeof(void*);
			var type = exterior;
			while (type.HasElementType) {
				if (type.IsPointer) {
					if (directVoids || type != voidPtrType) {
						transform.AddFirst(MakePointerType);
						type = type.GetElementType();
					}
					else {
						break;
					}
				}
				else if (type.IsByRef) {
					transform.AddFirst(MakeByRefType);
					type = type.GetElementType();
				}
				else if (type.IsArray) {
					transform.AddFirst(MakeArrayType);
					type = type.GetElementType();
				}
				else
					throw new NotImplementedException();
			}
			return type;
		}
		
		public static Type MakeSplitPointerType(Type p, Type t32, Type t64)
			=> typeof(SplitPointer<,,>).MakeGenericType(p, t32, t64);
	}
}