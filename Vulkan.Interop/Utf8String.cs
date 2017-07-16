using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Interop {
	using static Utf8StringInternal;
	public unsafe struct Utf8String :
		IEquatable<string>, IComparable<string>,
		IEquatable<Utf8String>, IComparable<Utf8String>,
		IReadOnlyList<sbyte>, IReadOnlyCollection<char>,
		IConvertible {

		public static sbyte* GetPointer(string value)
			=> new Utf8String(value);

		public readonly sbyte* Pointer;

		public IReadOnlyList<sbyte> SBytes => this;
		
		sbyte IReadOnlyList<sbyte>.this[int index] {
			get {
				if (index < 0 || index > ByteLength)
					return 0;
				return Pointer[index];
			}
		}

		public Utf8String(sbyte* sBytes) => Pointer = sBytes;

		public Utf8String(sbyte* sBytes, uint length)
			: this(sBytes) {
			ByteLengthCache[this] = length;
		}

		public Utf8String(string str) {
#if NETSTANDARD2_0
			if ( string.IsInterned(str) != null
				&& Interned.TryGetValue(str, out var duplicate)) {
				Pointer = duplicate.Pointer;
				return;
			}
#endif

			var utf8 = Encoding.UTF8;
			var byteCount = utf8.GetByteCount(str);
			var allocSize = byteCount + 1;
			Pointer = (sbyte*) Marshal.AllocHGlobal(allocSize);
			//Pointer = (sbyte*) Marshal.AllocCoTaskMem(allocSize);
			fixed (char* pch = str) {
				utf8.GetBytes(pch, str.Length, (byte*) Pointer, byteCount);
				Pointer[byteCount] = 0;
			}
			Allocated[this] = (uint)allocSize;
			//ByteLengthCache[this] = (uint)byteCount;
			CharCountCache[this] = (uint) str.Length;
			StringCache[this] = str;

		}

		public uint ByteLength
			=> Allocated.TryGetValue(this, out var length)
				? length-1
				: ByteLengthCache.TryGetValue(this, out length)
					? length
					: CountBytes();

		public uint CharCount
			=> CharCountCache.TryGetValue(this, out var count)
				? count
				: CountChars();

		public override int GetHashCode()
			=> HashCodeCache.TryGetValue(this, out var hash)
				? hash
				: CalculateHashCode();

		private int CalculateHashCode()
			=> HashCodeCache[this] = (int) XxHash32.CalculateHash((byte*) Pointer, ByteLength);

		public override string ToString()
			=> StringCache.TryGetValue(this, out var str)
				? str
				: Decode();

		public void Refresh() {
			StringCache.TryRemove(this, out var _);
			HashCodeCache.TryRemove(this, out var _);
			CharCountCache.TryRemove(this, out var _);
			ByteLengthCache.TryRemove(this, out var _);
		}

		private uint CountBytes() {
			var length = 0;
			while (Pointer[length] != 0)
				++length;

			return ByteLengthCache[this] = (uint)length;
		}

		private uint CountChars() {
			try {
				var count = (uint) Encoding.UTF8.GetCharCount((byte*) Pointer, (int) ByteLength);
				CharCountCache[this] = count;
				return count;
			}
			catch (Exception ex) {
				throw new NotImplementedException("Can't count characters in this Utf8String!",ex);
			}
		}

		private string Decode() {
			try {
				var s = Encoding.UTF8.GetString((byte*) Pointer, (int) ByteLength);
				StringCache[this] = s;
#if NETSTANDARD2_0
			if ( string.IsInterned(s) != null )
				Interned[s] = this;
#endif
				return s;
			}
			catch (Exception ex) {
				throw new NotImplementedException("Can't decode characters in this Utf8String!",ex);
			}
		}

		IEnumerator<char> IEnumerable<char>.GetEnumerator() {
			var decoder = Encoding.UTF8.GetDecoder();
			var byteBuf = new byte[1];
			var charBuf = new char[1];
			int charsRead;
			foreach (var sb in (IEnumerable<sbyte>) this) {
				charsRead = decoder.GetChars(byteBuf, 0, 1, charBuf, 0, false);
				if (charsRead != 0)
					yield return charBuf[0];
			}
			do {
				charsRead = decoder.GetChars(byteBuf, 0, 0, charBuf, 0, true);
				if (charsRead != 0)
					yield return charBuf[0];
			} while (charsRead != 0);
		}

		IEnumerator<sbyte> IEnumerable<sbyte>.GetEnumerator() {
			for (var i = 0 ; i < ByteLength ; ++i) {
				var b = SBytes[i];
				if (b == 0)
					yield break;

				yield return b;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
			=> ((IEnumerable<char>) this).GetEnumerator();

		public int CompareTo(string other) {
			if (other == null) return 1;
			if (ByteLength > other.Length) return 1;
			if (ByteLength < other.Length) return -1;

			using (var thisChars = ((IEnumerable<char>) this).GetEnumerator())
			using (var otherChars = ((IEnumerable<char>) other).GetEnumerator())
				for (; ;) {
					var thisMoved = thisChars.MoveNext();
					var otherMoved = otherChars.MoveNext();
					if (thisMoved && otherMoved) {
						var charComparison = thisChars.Current
							.CompareTo(otherChars.Current);

						if (charComparison == 0)
							continue;

						return charComparison;
					}

					if (!thisMoved && !otherMoved)
						return 0;

					if (!thisMoved)
						return -1;

					// !otherMoved
					return 1;
				}
		}

		public bool Equals(string other)
			=> CompareTo(other) == 0;

		public int CompareTo(Utf8String other) {
			if (default(sbyte*) == other.Pointer) return 1;
			if (Pointer == other.Pointer) return 0;

			var length = ByteLength;
			var otherLength = other.ByteLength;
			if (length > otherLength) return 1;
			if (length < otherLength) return -1;
			
			for (var i = 0 ; i < length ; ++i) {
				var thisSByte = SBytes[i];
				var otherSByte = other.SBytes[i];
				var sbyteComparison = thisSByte - otherSByte;
				if (sbyteComparison == 0 && thisSByte == 0)
					return 0;

				if (sbyteComparison == 0)
					continue;

				return sbyteComparison;
			}

			return 0;
		}

		public bool Equals(Utf8String other)
			=> CompareTo(other) == 0;

		public bool Equals(sbyte* other)
			=> Pointer == other;

		public TypeCode GetTypeCode() => TypeCode.String;

		public bool ToBoolean(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToBoolean(provider);

		public byte ToByte(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToByte(provider);

		public char ToChar(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToChar(provider);

		public DateTime ToDateTime(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToDateTime(provider);

		public decimal ToDecimal(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToDecimal(provider);

		public double ToDouble(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToDouble(provider);

		public short ToInt16(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToInt16(provider);

		public int ToInt32(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToInt32(provider);

		public long ToInt64(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToInt64(provider);

		public sbyte ToSByte(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToSByte(provider);

		public float ToSingle(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToSingle(provider);

		public string ToString(IFormatProvider provider)
			=> ToString();

		public object ToType(Type conversionType, IFormatProvider provider)
			=> ((IConvertible) ToString()).ToType(conversionType, provider);

		public ushort ToUInt16(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToUInt16(provider);

		public uint ToUInt32(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToUInt32(provider);

		public ulong ToUInt64(IFormatProvider provider)
			=> ((IConvertible) ToString()).ToUInt64(provider);

		public static implicit operator string(Utf8String s)
			/* ReSharper disable once SpecifyACultureInStringConversionExplicitly */
			=> s.ToString();

		public static implicit operator Utf8String(string s)
			=> new Utf8String(s);

		public static implicit operator Utf8String(sbyte* s)
			=> new Utf8String(s);

		public static implicit operator sbyte*(Utf8String s)
			=> s.Pointer;

		public static implicit operator void*(Utf8String s)
			=> s.Pointer;

		public static implicit operator IntPtr(Utf8String s)
			=> (IntPtr)s.Pointer;

		public static implicit operator UIntPtr(Utf8String s)
			=> (UIntPtr)s.Pointer;

		int IReadOnlyCollection<sbyte>.Count
			=> (int) ByteLength;

		int IReadOnlyCollection<char>.Count
			=> (int) CharCount;

	}
}