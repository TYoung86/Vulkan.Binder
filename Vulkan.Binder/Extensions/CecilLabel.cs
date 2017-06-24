using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;

namespace Vulkan.Binder.Extensions {
	public class CecilLabel {
		private readonly ILProcessor _ilp;

		private readonly LinkedList<Instruction> _emitted
			= new LinkedList<Instruction>();

		private Instruction _placeholder;

		private Instruction _target;

		public CecilLabel(ILProcessor ilp) {
			_ilp = ilp;
		}

		private bool PlaceholderSet => _placeholder != null;
		private bool TargetSet => _target != null;

		public bool IsSameILProcessor(ILProcessor ilp) {
			return _ilp == ilp;
		}

		public void Emit(OpCode opCode) {
			if (TargetSet) {
				_ilp.Emit(opCode, _target);
				return;
			}
			if (PlaceholderSet) {
				_ilp.Emit(opCode, _placeholder);
			}
			else {
				var needsFirstInstrFixup = false;
				var instrs = _ilp.Body.Instructions;
				var firstInstr = Enumerable.FirstOrDefault(instrs);
				if (firstInstr == null) {
					needsFirstInstrFixup = true;
					_ilp.Emit(OpCodes.Nop);
					firstInstr = Enumerable.First(instrs);
				}
				_ilp.Emit(opCode, firstInstr);
				var emitted = Enumerable.Last(instrs);
				if (needsFirstInstrFixup) {
					emitted.Operand = emitted;
					instrs.RemoveAt(0);
				}
			}
			_emitted.AddLast(Enumerable.Last(_ilp.Body.Instructions));
		}

		public void Mark() {
			_ilp.Emit(OpCodes.Nop);
			_placeholder = Enumerable.Last(_ilp.Body.Instructions);

			foreach (var instr in _emitted)
				instr.Operand = _placeholder;
		}


		public bool Cleanup() {
			var next = _placeholder?.Next;
			if (next == null)
				return _target != null;

			if (next.OpCode == OpCodes.Nop)
				return false;
			_target = next;
			foreach (var instr in _emitted)
				instr.Operand = _target;
			_emitted.Clear();
			_ilp.Body.Instructions.Remove(_placeholder);
			_placeholder = null;
			return true;
		}
	}
}