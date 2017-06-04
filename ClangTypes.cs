using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ClangSharp;

namespace Artilect.Vulkan.Binder {
	public interface IClangType {
		string Name { get; }
	}

	public sealed class RoughClangTypeComparer : IEqualityComparer<IClangType> {
		public bool Equals(IClangType x, IClangType y) {
			return StructuralComparisons.StructuralEqualityComparer.Equals(x, y);
		}

		public int GetHashCode(IClangType obj) {
			throw new NotImplementedException();
		}
	}

	public sealed class RoughCxTypeComparer : IEqualityComparer<CXType> {
		public bool Equals(CXType x, CXType y) {
			return x.kind == y.kind
					&& string.Equals(x.ToString(), y.ToString(), StringComparison.Ordinal);
		}

		public int GetHashCode(CXType obj) {
			return obj.ToString().GetHashCode();
		}

		private static readonly RoughCxTypeComparer Instance
			= new RoughCxTypeComparer();

		public static bool CompareEquality(CXType x, CXType y) {
			return Instance.Equals(x, y);
		}
	}

	public abstract class ClangFieldInfoBase : IClangType, IEquatable<ClangFieldInfoBase> {
		public override string ToString() {
			return $"{Type} {Name}";
		}

		public abstract bool IsParameter { get; }
		public CXType Type { get; }
		public string Name { get; }

		protected ClangFieldInfoBase(CXType type, string name) {
			Type = type;
			Name = name;
		}

		public bool Equals(ClangFieldInfoBase other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return RoughCxTypeComparer.CompareEquality(Type, other.Type)
					&& string.Equals(Name, other.Name, StringComparison.Ordinal);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ClangFieldInfoBase) obj);
		}

		public override int GetHashCode() {
			unchecked {
				return (Type.GetHashCode() * 397) ^ (Name != null ? Name.GetHashCode() : 0);
			}
		}

		public static bool operator ==(ClangFieldInfoBase left, ClangFieldInfoBase right) => Equals(left, right);

		public static bool operator !=(ClangFieldInfoBase left, ClangFieldInfoBase right) => !Equals(left, right);
	}

	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
	public class ClangFieldInfo : ClangFieldInfoBase {
		private string DebuggerDisplay
			=> $"{Type} {Name} (+{Offset})";

		public override bool IsParameter => false;
		public uint Offset { get; }

		public ClangFieldInfo(CXType type, string name, uint offset)
			: base(type, name) {
			Offset = offset;
		}
	}

	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
	public sealed class ClangParameterInfo : ClangFieldInfoBase {
		private string DebuggerDisplay
			=> $"{Type} {Name} (#{Index})";

		public override bool IsParameter => true;
		public uint Index { get; }

		public ClangParameterInfo(CXType type, string name, uint index)
			: base(type, name) {
			Index = index;
		}
	}

	public abstract class ClangStructInfoBase : IClangType, IEquatable<ClangStructInfoBase> {
		public bool Equals(ClangStructInfoBase other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return string.Equals(Name, other.Name, StringComparison.Ordinal)
					&& Size == other.Size
					&& Alignment == other.Alignment
					&& Fields.SequenceEqual(other.Fields);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ClangStructInfoBase) obj);
		}

		public override int GetHashCode() {
			unchecked {
				var hashCode = Name != null ? Name.GetHashCode() : 0;
				hashCode = (hashCode * 397) ^ Fields.GetHashCode();
				hashCode = (hashCode * 397) ^ (int) Size;
				hashCode = (hashCode * 397) ^ (int) Alignment;
				return hashCode;
			}
		}

		public static bool operator ==(ClangStructInfoBase left, ClangStructInfoBase right) => Equals(left, right);

		public static bool operator !=(ClangStructInfoBase left, ClangStructInfoBase right) => !Equals(left, right);

		public abstract bool IsUnion { get; }
		public string Name { get; }
		public ImmutableArray<ClangFieldInfo> Fields { get; }
		public uint Size { get; }
		public uint Alignment { get; }

		protected ClangStructInfoBase(string name, IEnumerable<ClangFieldInfo> fields, uint size, uint alignment) {
			Name = name;
			Fields = fields.ToImmutableArray();
			Size = size;
			Alignment = alignment;
		}
	}

	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
	public sealed class ClangStructInfo : ClangStructInfoBase {
		private string DebuggerDisplay
			=> $"struct {Name} ({Size} @ {Alignment}, {Fields.Length} fields))";

		public override bool IsUnion => false;

		public ClangStructInfo(string name, IEnumerable<ClangFieldInfo> fields, uint size, uint alignment)
			: base(name, fields, size, alignment) {
		}
	}

	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
	public sealed class ClangUnionInfo : ClangStructInfoBase {
		private string DebuggerDisplay
			=> $"union {Name} ({Size} @ {Alignment}, {Fields.Length} fields))";

		public override bool IsUnion => false;

		public ClangUnionInfo(string name, IEnumerable<ClangFieldInfo> fields, uint size, uint alignment)
			: base(name, fields, size, alignment) {
		}
	}

	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
	public abstract class ClangFunctionInfoBase : IClangType, IEquatable<ClangFunctionInfoBase> {
		private string DebuggerDisplay
			=> $"({CallConvention}) {ReturnType} {Name}({string.Join(", ", Parameters)})";

		public bool Equals(ClangFunctionInfoBase other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return RoughCxTypeComparer.CompareEquality(ReturnType, other.ReturnType)
					&& string.Equals(Name, other.Name, StringComparison.Ordinal)
					&& Parameters.SequenceEqual(other.Parameters);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ClangFunctionInfoBase) obj);
		}

		public override int GetHashCode() {
			unchecked {
				var hashCode = (int) CallConvention;
				hashCode = (hashCode * 397) ^ ReturnType.ToString().GetHashCode();
				hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ Parameters.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(ClangFunctionInfoBase left, ClangFunctionInfoBase right) => Equals(left, right);

		public static bool operator !=(ClangFunctionInfoBase left, ClangFunctionInfoBase right) => !Equals(left, right);

		public abstract bool IsPointer { get; }
		public CXCallingConv CallConvention { get; }
		public CXType ReturnType { get; }
		public string Name { get; }
		public ImmutableArray<ClangParameterInfo> Parameters { get; }

		protected ClangFunctionInfoBase(CXCallingConv callConv, CXType retType, string name, IEnumerable<ClangParameterInfo> parameters) {
			CallConvention = callConv;
			ReturnType = retType;
			Name = name;
			Parameters = parameters.ToImmutableArray();
		}
	}

	public sealed class ClangFunctionInfo : ClangFunctionInfoBase {
		public override bool IsPointer => false;

		public ClangFunctionInfo(CXCallingConv callConv, CXType retType, string name, IEnumerable<ClangParameterInfo> parameters)
			: base(callConv, retType, name, parameters) {
		}

		public ClangFunctionInfo(CXCallingConv callConv, CXType retType, string name, params ClangParameterInfo[] parameters)
			: base(callConv, retType, name, parameters) {
		}
	}

	public sealed class ClangDelegateInfo : ClangFunctionInfoBase {
		public override bool IsPointer => true;

		public ClangDelegateInfo(CXCallingConv callConv, CXType retType, string name, IEnumerable<ClangParameterInfo> parameters)
			: base(callConv, retType, name, parameters) {
		}

		public ClangDelegateInfo(CXCallingConv callConv, CXType retType, string name, params ClangParameterInfo[] parameters)
			: base(callConv, retType, name, parameters) {
		}
	}

	[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "}")]
	public class ClangConstantInfoBase : IClangType, IEquatable<ClangConstantInfoBase> {
		private string DebuggerDisplay
			=> $"{Name} = {Value}";

		public bool Equals(ClangConstantInfoBase other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return string.Equals(Name, other.Name, StringComparison.Ordinal)
					&& Equals(Value, other.Value);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ClangConstantInfoBase) obj);
		}

		public override int GetHashCode() {
			unchecked {
				return ((Name != null ? Name.GetHashCode() : 0) * 397)
						^ (Value != null ? Value.GetHashCode() : 0);
			}
		}

		public static bool operator ==(ClangConstantInfoBase left, ClangConstantInfoBase right) => Equals(left, right);

		public static bool operator !=(ClangConstantInfoBase left, ClangConstantInfoBase right) => !Equals(left, right);

		public string Name { get; }
		public object Value { get; }

		protected ClangConstantInfoBase(string name, object value) {
			Name = name;
			Value = value;
		}
	}

	public abstract class ClangConstantInfo : ClangConstantInfoBase {
		public ClangConstantInfo(string name, object value)
			: base(name, value) {
		}
	}

	public sealed class ClangConstantInfo<T> : ClangConstantInfo {
		public new T Value => (T) base.Value;

		public ClangConstantInfo(string name, T value)
			: base(name, value) {
		}
	}

	public abstract class ClangEnumInfoBase : IClangType, IEquatable<ClangEnumInfoBase> {
		public bool Equals(ClangEnumInfoBase other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return RoughCxTypeComparer.CompareEquality(UnderlyingType, other.UnderlyingType)
					&& string.Equals(Name, other.Name, StringComparison.Ordinal)
					&& Definitions.SequenceEqual(other.Definitions);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;
			return Equals((ClangEnumInfoBase) obj);
		}

		public override int GetHashCode() {
			unchecked {
				var hashCode = UnderlyingType.GetHashCode();
				hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ Definitions.GetHashCode();
				return hashCode;
			}
		}

		public static bool operator ==(ClangEnumInfoBase left, ClangEnumInfoBase right) => Equals(left, right);

		public static bool operator !=(ClangEnumInfoBase left, ClangEnumInfoBase right) => !Equals(left, right);

		public abstract bool IsFlags { get; }
		public CXType UnderlyingType { get; }
		public string Name { get; }
		public ImmutableArray<ClangConstantInfo> Definitions { get; }

		protected ClangEnumInfoBase(CXType underlyingType, string name, IEnumerable<ClangConstantInfo> definitions) {
			UnderlyingType = underlyingType;
			Name = name;
			Definitions = definitions.ToImmutableArray();
		}
	}

	public sealed class ClangEnumInfo : ClangEnumInfoBase {
		public override bool IsFlags => false;

		public ClangEnumInfo(CXType underlyingType, string name, IEnumerable<ClangConstantInfo> definitions)
			: base(underlyingType, name, definitions) {
		}

		public ClangEnumInfo(CXType underlyingType, string name, params ClangConstantInfo[] definitions)
			: base(underlyingType, name, definitions) {
		}
	}

	public sealed class ClangFlagsInfo : ClangEnumInfoBase {
		public override bool IsFlags => true;

		public ClangFlagsInfo(CXType underlyingType, string name, IEnumerable<ClangConstantInfo> definitions)
			: base(underlyingType, name, definitions) {
		}

		public ClangFlagsInfo(CXType underlyingType, string name, params ClangConstantInfo[] definitions)
			: base(underlyingType, name, definitions) {
		}
	}
}