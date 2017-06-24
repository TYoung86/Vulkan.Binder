using System;
using System.Collections.Generic;
using System.Linq;
using ClangSharp;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseEnum(CXCursor cursor) {
			IncrementStatistic("enums");
			var type = clang.getEnumDeclIntegerType(cursor);
			var typeKind = type.kind;
			var isSigned = SignedCxTypeKinds.Contains(typeKind);

			var name = cursor.ToString();

			if (name == null)
				throw new NotImplementedException("Handling of unnamed enumerations are not implemented.");

			ICollection<ClangConstantInfo> defs
				= new LinkedList<ClangConstantInfo>();

			clang.visitChildren(cursor,
				(current, parent, p) => {
					var constName = current.ToString();

					if (string.IsNullOrEmpty(constName))
						throw new NotImplementedException("Handling of unnamed enumeration literals are not implemented.");

					defs.Add(isSigned
						? (ClangConstantInfo) new ClangConstantInfo<long>(constName,
							clang.getEnumConstantDeclValue(current))
						: new ClangConstantInfo<ulong>(constName,
							clang.getEnumConstantDeclUnsignedValue(current)));
					return CXChildVisitResult.CXChildVisit_Continue;
				},
				default(CXClientData));

			return new ClangEnumInfo(type, name, defs.ToArray());
		}
	}
}