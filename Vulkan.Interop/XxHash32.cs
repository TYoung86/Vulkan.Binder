using System.Runtime.CompilerServices;

/*
xxHash Library
Copyright (c) 2012-2014, Yann Collet
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this
  list of conditions and the following disclaimer in the documentation and/or
  other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Interop {
	public unsafe struct XxHash32 {

		private const uint Prime1 = 2654435761U;

		private const uint Prime2 = 2246822519U;

		private const uint Prime3 = 3266489917U;

		private const uint Prime4 = 668265263U;

		private const uint Prime5 = 374761393U;

		
		public static uint CalculateHash(byte[] input, uint seed = 0) {
			fixed (byte* pInput = &input[0])
				return CalculateHash(pInput, (uint)input.Length, seed);
		}

		public static uint CalculateHash(byte[] input, uint offset, uint length, uint seed = 0) {
			fixed (byte* pInput = &input[offset])
				return CalculateHash(pInput, length, seed);
		}

		public static uint CalculateHash(byte* pInput, uint length, uint seed = 0) {
			return new XxHash32(pInput, length, seed).Digest();
		}

		private uint _totalLength;

		private readonly uint _seed;

		private uint _component1;

		private uint _component2;

		private uint _component3;

		private uint _component4;

		private uint _bufferSize;
		
		private readonly byte[] _buffer;

		public XxHash32(uint seed = 0) {
			_seed = seed;
			_component1 = seed + Prime1 + Prime2;
			_component2 = seed + Prime2;
			_component3 = seed;
			_component4 = seed - Prime1;
			_totalLength = 0;
			_bufferSize = 0;
			_buffer = new byte[16];
		}
		public XxHash32(byte[] input, uint offset, uint length, uint seed = 0)
			: this(seed) {
			fixed (byte* pInput = &input[offset])
				Update(pInput, length);
		}

		public XxHash32(byte[] input, uint seed = 0)
			: this(seed) {
			fixed (byte* pInput = &input[0])
				Update(pInput, (uint) input.Length);
		}

		public XxHash32(byte* pInput, uint length, uint seed = 0)
			: this(seed)
			=> Update(pInput, length);

		public void Update(byte[] input, uint length) {
			fixed (byte* pInput = &input[0])
				Update(pInput, length);
		}

		public void Update(byte* pInput, uint length) {
			fixed (byte* pBuffer = &_buffer[0]) {
				_totalLength += length;

				if (_bufferSize + length < 16) {
					Unsafe.CopyBlock(pInput, pBuffer + _bufferSize, length);
					_bufferSize += length;

					return;
				}

				if (_bufferSize > 0) {
					Unsafe.CopyBlock(pInput, pBuffer + _bufferSize, 16 - _bufferSize);

					_component1 = CalcSubHash(_component1, *(uint*) &pBuffer[0]);
					_component2 = CalcSubHash(_component2, *(uint*) &pBuffer[4]);
					_component3 = CalcSubHash(_component3, *(uint*) &pBuffer[8]);
					_component4 = CalcSubHash(_component4, *(uint*) &pBuffer[12]);

					_bufferSize = 0;
				}

				var index = 0u;
				if (index <= length - 16) {
					var limit = length - 16;
					var component1 = _component1;
					var component2 = _component2;
					var component3 = _component3;
					var component4 = _component4;

					do {
						component1 = CalcSubHash(component1, *(uint*) &pInput[index]);
						component2 = CalcSubHash(component2, *(uint*) &pInput[index + 4]);
						component3 = CalcSubHash(component3, *(uint*) &pInput[index + 8]);
						component4 = CalcSubHash(component4, *(uint*) &pInput[index + 12]);
						index += 16;
					} while (index <= limit);

					_component1 = component1;
					_component2 = component2;
					_component3 = component3;
					_component4 = component4;
				}

				if (index >= length)
					return;

				Unsafe.CopyBlock(pInput + index, pBuffer, length - index);

				_bufferSize = length - index;
			}
		}

		public uint Digest() {
			uint value;
			if (_totalLength >= 16) {
				value = RotateLeft(_component1, 1)
						+ RotateLeft(_component2, 7)
						+ RotateLeft(_component3, 12)
						+ RotateLeft(_component4, 18);
			}
			else
				value = _seed + Prime5;

			value += _totalLength;

			var index = 0;
			fixed (byte* pBuffer = &_buffer[0]) {
				while (index <= _bufferSize - 4) {
					value += *(uint*) &pBuffer[index] * Prime3;
					value = RotateLeft(value, 17) * Prime4;
					index += 4;
				}

				while (index < _bufferSize) {
					value += pBuffer[index] * Prime5;
					value = RotateLeft(value, 11) * Prime1;
					++index;
				}
			}

			value ^= value >> 15;
			value *= Prime2;
			value ^= value >> 13;
			value *= Prime3;
			value ^= value >> 16;

			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint CalcSubHash(uint value, uint input)
			=> RotateLeft(value + input * Prime2, 13) * Prime1;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint RotateLeft(uint value, int count)
			=> (value << count) | (value >> (32 - count));

	}
}