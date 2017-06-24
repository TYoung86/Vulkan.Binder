using System;
using ClangSharp;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseFunction(CXCursor cursor) {
			IncrementStatistic("functions");
			var name = cursor.ToString();

			if (name == null)
				throw new NotImplementedException("Handling of unnamed functions are not implemented.");
			
			/*
			if (TypeRedirects.TryGetValue(name, out var renamed)) {
				name = renamed;
			}
			*/

			var funcType = clang.getCursorType(cursor);

			var retType = clang.getCursorResultType(cursor);

			var argTypeCount = clang.getNumArgTypes(funcType);

			var paramInfos = new ClangParameterInfo[argTypeCount];

			for (var i = 0u ; i < argTypeCount ; ++i) {
				var argCursor = clang.Cursor_getArgument(cursor, i);
				var argType = clang.getArgType(funcType, i);
				var paramName = argCursor.ToString();
				if (string.IsNullOrEmpty(paramName))
					paramName = "_" + i;
				paramInfos[i] = new ClangParameterInfo(argType, paramName, i);
			}

			var callConv = clang.getFunctionTypeCallingConv(funcType);

			return new ClangFunctionInfo(callConv, retType, name, paramInfos);
		}
	}
}