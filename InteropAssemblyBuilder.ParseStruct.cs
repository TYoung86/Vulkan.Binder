using System;
using System.Collections.Generic;
using System.Linq;
using ClangSharp;

namespace Artilect.Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseStruct(CXCursor cursor) {
			IncrementStatistic("structs");
			var name = cursor.ToString();

			if (name == null)
				throw new NotImplementedException("Handling of unnamed structs are not implemented.");


			var type = clang.getCursorType(cursor);

			var fields = new LinkedList<ClangFieldInfo>();

			var alignment = (uint) Math.Max(0, clang.Type_getAlignOf(type));

			var size = (uint) Math.Max(0, clang.Type_getSizeOf(type));

			/*
			var typeDef = Module.DefineType(name,
				TypeAttributes.Sealed | TypeAttributes.Public | TypeAttributes.SequentialLayout,
				null, typeAlign, typeSize);
			*/

			clang.Type_visitFields(type, (fieldCursor, p) => {
				var fieldName = fieldCursor.ToString();
				var fieldType = clang.getCursorType(fieldCursor);
				var fieldOffset = (uint) clang.Cursor_getOffsetOfField(fieldCursor);
				fields.AddLast(new ClangFieldInfo(fieldType, fieldName, fieldOffset));
				return CXVisitorResult.CXVisit_Continue;
			}, default(CXClientData));

			return new ClangStructInfo(name, fields.ToArray(), size, alignment);
		}
	}
}