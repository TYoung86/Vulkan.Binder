using System;
using ClangSharp;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private IClangType ParseDelegate(CXCursor cursor, CXCallingConv callConv) {
			IncrementStatistic("delegates");
			var name = cursor.ToString();
			var pfnType = clang.getTypedefDeclUnderlyingType(cursor);
			var funcType = clang.getPointeeType(pfnType);
			var argTypeCount = clang.getNumArgTypes(funcType);
			var retType = clang.getResultType(funcType);
			//var clrRetType = ResolveParameter(retType);
			var paramInfos = new ClangParameterInfo[argTypeCount];
			var i = 0u;
			clang.visitChildren(cursor, (paramCursor, parent, p) => {
				if (paramCursor.kind != CXCursorKind.CXCursor_ParmDecl) {
					// return type
					if (i == 0 && paramCursor.kind == CXCursorKind.CXCursor_TypeRef)
						return CXChildVisitResult.CXChildVisit_Continue;
					throw new NotImplementedException();
				}
				var paramType = clang.getCursorType(paramCursor);
				var paramName = paramCursor.ToString();
				if (string.IsNullOrEmpty(paramName))
					paramName = "_" + i;
				//var clrArgParam = ResolveParameter(argType, paramName);
				if (i >= argTypeCount)
					throw new NotImplementedException();
				paramInfos[i] = new ClangParameterInfo(paramType, paramName, i);
				++i;
				return CXChildVisitResult.CXChildVisit_Continue;
			}, default(CXClientData));

			/*
				var funcDef = Module.DefineType(name,
					TypeAttributes.Sealed | TypeAttributes.Public,
					typeof(MulticastDelegate));
				*/
			return new ClangDelegateInfo(callConv, retType, name, paramInfos);
		}
	}
}