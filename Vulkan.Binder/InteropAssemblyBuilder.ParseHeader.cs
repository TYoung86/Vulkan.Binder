using System;
using System.Collections.Generic;
using ClangSharp;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		public void ParseHeader(string header) {
			// TODO: store / use unsaved ?
			// ReSharper disable once UnusedVariable
			var unit32 = ParseHeader(header, out var unsaved32, 32)
						?? throw new InvalidProgramException($"Unable to parse header {header} for 32-bit");

			if (!Units32.TryAdd(header, unit32)) {
				throw new NotImplementedException();
			}

			// ReSharper disable once UnusedVariable
			var unit64 = ParseHeader(header, out var unsaved64, 64)
						?? throw new InvalidProgramException($"Unable to parse header {header} for 64-bit");


			if (!Units64.TryAdd(header, unit64)) {
				throw new NotImplementedException();
			}
		}

		private CXTranslationUnit? ParseHeader(string header, out CXUnsavedFile unsaved, int bits) {
			var args = new[] {
				"-x", "c-header", $"-m{bits}"
			};

			var error = clang.parseTranslationUnit2(
				bits == 32
					? ClangIndex32
					: bits == 64
						? ClangIndex64
						: throw new NotImplementedException(),
				header,
				args, args.Length,
				out unsaved, 0,
				0, out CXTranslationUnit unit);

			var success = error == CXErrorCode.CXError_Success;

			if (success)
				return unit;


			var numDiagnostics = clang.getNumDiagnostics(unit);

			ICollection<string> diagMsgs = new LinkedList<string>();
			for (uint i = 0 ; i < numDiagnostics ; ++i) {
				var diagnostic = clang.getDiagnostic(unit, i);
				diagMsgs.Add(clang.getDiagnosticSpelling(diagnostic).ToString());
				clang.disposeDiagnostic(diagnostic);
			}

			throw new InvalidProgramException($"Clang {error} parsing {header} for {bits}-bit") {
				Data = {
					{nameof(error), error},
					{nameof(header), header},
					{nameof(bits), bits},
					{nameof(diagMsgs), diagMsgs}
				}
			};
		}
	}
}