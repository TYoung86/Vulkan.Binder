using System;

namespace Interop {
	public struct Bool64 : IEquatable<bool>,  IEquatable<Bool16>, IEquatable<Bool32>, IEquatable<Bool64> {
		private readonly uint _value;

		public bool Value => _value != 0;
		
		public Bool64(uint value) => _value = value;
		public Bool64(bool value) => _value = value ? 1u : 0;

		public bool Equals(bool other) => Value == other;
		public bool Equals(Bool16 other) => Value == other;
		public bool Equals(Bool32 other) => Value == other;
		public bool Equals(Bool64 other) => Value == other;

		public override int GetHashCode() => Value ? 1 : 0;

		public static implicit operator bool(Bool64 b) => b.Value;
		public static implicit operator Bool64(bool b) => new Bool64(b);
	}
}