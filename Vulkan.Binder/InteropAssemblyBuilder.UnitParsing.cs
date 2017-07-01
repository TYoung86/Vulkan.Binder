using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClangSharp;
using Mono.Cecil;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		public void ParseUnits() {
			var headers = new HashSet<string>(Units32.Keys);
			if (!headers.SetEquals(Units64.Keys))
				throw new NotImplementedException();

			var parseResults32 = new ConcurrentDictionary<string, IClangType>();
			var parseResults64 = new ConcurrentDictionary<string, IClangType>();
			ICollection<Task> tasks = new LinkedList<Task>();
			var index = 0;
			var total = headers.Count * 2;
			foreach (var header in headers) {
				tasks.Add(Task.Run(() => {
					ParseUnit(Units32[header], parseResults32);
					ReportProgress("Parsing units", Interlocked.Increment(ref index)-1, total);
				}));
				tasks.Add(Task.Run(() => {
					ParseUnit(Units64[header], parseResults64);
					ReportProgress("Parsing units", Interlocked.Increment(ref index)-1, total);
				}));
			}
			Task.WaitAll(tasks.ToArray());
			ReportProgress("Parsing units", index, total);

			var symbols32 = ImmutableHashSet.Create(parseResults32.Keys.ToArray());
			var symbols64 = ImmutableHashSet.Create(parseResults64.Keys.ToArray());

			var allSymbols = symbols32.Union(symbols64);
			//var oddSymbols = symbols32.SymmetricExcept(symbols64);

			// in vulkan, there are non-dispatchable 64-bit handles that are c14n'd away in 32-bit
			var symbols32Only = symbols32.Except(symbols64);
			var symbols64Only = symbols64.Except(symbols32);

			var oddSymbols = symbols32Only.Union(symbols64Only);

			index = 0;
			total = allSymbols.Count;

			foreach (var oddSymbol in oddSymbols) {
				ReportProgress("Preparing definitions", index++, total);
				parseResults32.TryGetValue(oddSymbol, out var parseResult32);
				parseResults64.TryGetValue(oddSymbol, out var parseResult64);
				CollectDefinitionFunc((Func<TypeDefinition[]>) DefineClrType((dynamic) parseResult32, (dynamic) parseResult64));
				//throw new NotImplementedException();
			}

			var evenSymbols = allSymbols.Except(oddSymbols);

			foreach (var evenSymbol in evenSymbols) {
				ReportProgress("Preparing definitions", index++, total);
				var parseResult32 = parseResults32[evenSymbol];
				var parseResult64 = parseResults64[evenSymbol];
				if (parseResult32.Equals(parseResult64)) {
					CollectDefinitionFunc((Func<TypeDefinition[]>) DefineClrType((dynamic) parseResult64));
				}
				else {
					CollectDefinitionFunc((Func<TypeDefinition[]>) DefineClrType((dynamic) parseResult32, (dynamic) parseResult64));
				}
				//throw new NotImplementedException();
			}
			ReportProgress("Preparing definitions", index, total);

			//BuildTypeDefinitions();

			//throw new NotImplementedException();
			
		}

		private void ParseUnit(CXTranslationUnit unit, ConcurrentDictionary<string, IClangType> parseResults) {
			var cursor = clang.getTranslationUnitCursor(unit);
			clang.visitChildren(cursor,
				(current, parent, p) => UnitParser(current, parseResults),
				default(CXClientData));
		}

		private CXChildVisitResult UnitParser(CXCursor cursor, ConcurrentDictionary<string, IClangType> parseResultBag) {
			void CollectResult(IClangType parseResult) {
				if (parseResult == null) return;
				if (!parseResultBag.TryAdd(parseResult.Name, parseResult))
					throw new NotImplementedException();
			}

			if (IsCursorInSystemHeader(cursor))
				return CXChildVisitResult.CXChildVisit_Continue;

			switch (clang.getCursorKind(cursor)) {
				case CXCursorKind.CXCursor_EnumDecl: {
					CollectResult(ParseEnum(cursor));
					break;
				}
				case CXCursorKind.CXCursor_FunctionDecl: {
					CollectResult(ParseFunction(cursor));
					break;
				}
				case CXCursorKind.CXCursor_TypedefDecl: {
					CollectResult(ParseTypeDef(cursor));
					break;
				}
				case CXCursorKind.CXCursor_StructDecl: {
					CollectResult(ParseStruct(cursor));
					break;
				}
				case CXCursorKind.CXCursor_UnionDecl: {
					CollectResult(ParseUnion(cursor));
					break;
				}
			}
			return CXChildVisitResult.CXChildVisit_Continue;
		}

		private static void CollectDefinitionFunc(Func<TypeDefinition[]> defFunc) {
			if (defFunc != null)
				DefinitionFuncs.Push(defFunc);
		}
	}
}