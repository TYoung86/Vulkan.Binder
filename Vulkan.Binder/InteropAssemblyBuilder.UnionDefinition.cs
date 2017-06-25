using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder {
	public partial class InteropAssemblyBuilder {
		private Func<TypeDefinition[]> DefineClrType(ClangUnionInfo unionInfo) {
			if (unionInfo.Size == 0) {
				throw new NotImplementedException();
			}
			var unionName = unionInfo.Name;
			if (TypeRedirects.TryGetValue(unionName, out var rename)) {
				unionName = rename;
			}
			TypeDefinition unionDef = Module.DefineType(unionName,
				PublicSealedUnionTypeAttributes, null,
				(int) unionInfo.Alignment,
				(int) unionInfo.Size);
			unionDef.SetCustomAttribute(() => new CompilerGeneratedAttribute());
			//unionDef.SetCustomAttribute(StructLayoutExplicitAttributeInfo);
			var fieldParams = new LinkedList<ParameterInfo>(unionInfo.Fields.Select(f => ResolveField(f.Type, f.Name, (int) f.Offset)));

			return () => {
				foreach (var fieldParam in fieldParams)
					fieldParam.RequireCompleteTypeReferences(TypeRedirects,true);

				fieldParams.ConsumeLinkedList(fieldParam => {
					var fieldName = fieldParam.Name;
					var fieldType = fieldParam.Type;
					if (fieldType is IncompleteTypeReference)
						throw new InvalidProgramException("Encountered incomplete type in structure field definition.");
					var fieldDef = unionDef.DefineField(fieldName, fieldType, FieldAttributes.Public);
					//fieldDef.SetCustomAttribute(AttributeInfo.Create(
					//	() => new FieldOffsetAttribute(fieldParam.Position)), Module);
					fieldDef.Offset = fieldParam.Position;
				});

				return new[] {unionDef.CreateType()};
			};
		}
	}
}