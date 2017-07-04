using System;

namespace Interop {
	public struct Bool16 : IEquatable<bool>,  IEquatable<Bool16>, IEquatable<Bool32>, IEquatable<Bool64> {
		private readonly uint _value;

		public bool Value => _value != 0;
		
		public Bool16(uint value) => _value = value;
		public Bool16(bool value) => _value = value ? 1u : 0;

		public bool Equals(bool other) => Value == other;
		public bool Equals(Bool16 other) => Value == other;
		public bool Equals(Bool32 other) => Value == other;
		public bool Equals(Bool64 other) => Value == other;

		public override int GetHashCode() => Value ? 1 : 0;

		public static implicit operator bool(Bool16 b) => b.Value;
		public static implicit operator Bool16(bool b) => new Bool16(b);
	}
}