using System;
using System.Diagnostics;
using Mono.Cecil;

namespace Vulkan.Binder {

	[DebuggerDisplay("~ {"+nameof(Type)+"} {"+nameof(Name)+"}")]
	public class ParameterInfo {
		public ParameterInfo(string name, TypeReference type, int position = -1, ParameterAttributes paramAttrs = default(ParameterAttributes), int arraySize = -1) {
			Type = type ?? throw new ArgumentNullException(nameof(type));
			Name = name ?? "";
			Position = position;
			ArraySize = arraySize;
			Attributes = paramAttrs;
		}
		
		public TypeReference Type;

		public string Name;

		public int Position;

		public int ArraySize;

		public ParameterAttributes Attributes;
	}
}