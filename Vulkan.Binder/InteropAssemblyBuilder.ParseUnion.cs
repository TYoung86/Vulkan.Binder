using System;
using System.Collections.Generic;
using System.Linq;
using ClangSharp;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseUnion(CXCursor cursor) {
			IncrementStatistic("unions");
			var name = cursor.ToString();

			if (name == null)
				throw new NotImplementedException("Handling of unnamed unions are not implemented.");

			var type = clang.getCursorType(cursor);

			var fields = new LinkedList<ClangFieldInfo>();

			var alignment = (uint) clang.Type_getAlignOf(type);

			var size = (uint) clang.Type_getSizeOf(type);
			/*
			var typeDef = Module.DefineType(name,
				TypeAttributes.Sealed | TypeAttributes.Public | TypeAttributes.ExplicitLayout,
				null, alignment, size);
				*/
			//var fieldPosition = 0;
			clang.Type_visitFields(type, (fieldCursor, p) => {
				var fieldName = fieldCursor.ToString();
				var fieldType = clang.getCursorType(fieldCursor);
				//var fieldDef = ResolveField(fieldType, fieldName);
				var fieldOffset = (uint) clang.Cursor_getOffsetOfField(fieldCursor);
				//fieldDef.AddCustomAttribute(() => new FieldOffsetAttribute(fieldOffset));
				//fieldDef.Position = fieldPosition;
				fields.AddLast(new ClangFieldInfo(fieldType, fieldName, fieldOffset));
				//++fieldPosition;
				return CXVisitorResult.CXVisit_Continue;
			}, default(CXClientData));

			return new ClangUnionInfo(name, fields.ToArray(), size, alignment);
		}
	}
}