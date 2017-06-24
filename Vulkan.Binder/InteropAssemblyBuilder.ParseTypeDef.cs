using System;
using ClangSharp;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseTypeDef(CXCursor cursor) {
			var originalType = clang.getCursorType(cursor);
			var canonType = clang.getCanonicalType(originalType);
			var typeDeclCursor = clang.getTypeDeclaration(canonType);
			if (IsCursorInSystemHeader(typeDeclCursor))
				return null;
			var name = cursor.ToString();
			if (typeDeclCursor.kind == CXCursorKind.CXCursor_NoDeclFound) {
				if (canonType.kind != CXTypeKind.CXType_Pointer) {
					// likely simple type alias
					if (TypeRedirects.TryGetValue(name, out var renamed)) {
						name = renamed;
					}
					if (KnownTypes.ContainsKey(name)) {
						if (PrimitiveTypeMap.TryGetValue(canonType.kind, out var primitiveType)) {
							var existingType = Module.GetType(name);
							if ( existingType == null )
								throw new NotImplementedException();
							existingType.ChangeUnderlyingType(primitiveType.Import(Module));
							IncrementStatistic("typedefs");
						}
						else {
							throw new NotImplementedException();
						}
					}
					return null;
				}
				var pointeeType = clang.getPointeeType(canonType);
				var callConv = clang.getFunctionTypeCallingConv(pointeeType);
				if (callConv == CXCallingConv.CXCallingConv_Invalid) {
					// likely a pointer type alias
					return null;
				}

				return ParseDelegate(cursor, callConv);
			}
			switch (typeDeclCursor.kind) {
				case CXCursorKind.CXCursor_UnionDecl:
				case CXCursorKind.CXCursor_StructDecl: {
					var typeName = typeDeclCursor.ToString();
					if (name == typeName)
						return null;
					throw new NotImplementedException();
				}
				case CXCursorKind.CXCursor_EnumDecl: {
					if (TypeRedirects.TryGetValue(name, out var renamed)) {
						name = renamed;
					}
					if (KnownTypes.TryGetValue(name, out var knownType)) {
						var existingType = Module.GetType(name);
						if (existingType != null)
							return null;
						switch (knownType) {
							case KnownType.Enum: {
								throw new NotImplementedException();
							}
							case KnownType.Bitmask: {
								throw new NotImplementedException();
							}
							default:
								throw new NotImplementedException();
						}
					}
					throw new NotImplementedException();
				}
			}
			IncrementStatistic("typedefs");
			Console.WriteLine(cursor.ToString());
			throw new NotImplementedException();
		}
	}
}