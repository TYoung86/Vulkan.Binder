using System;
using ClangSharp;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseTypeDef(CXCursor cursor) {
			var originalType = clang.getCursorType(cursor);
			var canonType = clang.getCanonicalType(originalType);
			var typeDeclCursor = clang.getTypeDeclaration(canonType);
			if (IsCursorInSystemHeader(typeDeclCursor))
				return null;
			if (typeDeclCursor.kind == CXCursorKind.CXCursor_NoDeclFound) {
				if (canonType.kind != CXTypeKind.CXType_Pointer) {
					// likely simple type alias
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
				case CXCursorKind.CXCursor_EnumDecl:
				case CXCursorKind.CXCursor_StructDecl:
				case CXCursorKind.CXCursor_UnionDecl: {
					var name = cursor.ToString();
					var typeName = typeDeclCursor.ToString();
					if (name == typeName)
						return null;
					throw new NotImplementedException();
				}
			}
			IncrementStatistic("typedefs");
			Console.WriteLine(cursor.ToString());
			throw new NotImplementedException();
		}
	}
}