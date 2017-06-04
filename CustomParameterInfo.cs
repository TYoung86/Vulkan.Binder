using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Artilect.Vulkan.Binder {
	[DebuggerDisplay("~ {"+nameof(ClassImpl)+"} {"+nameof(NameImpl)+"}")]
	public class CustomParameterInfo : ParameterInfo {
		public CustomParameterInfo(string name, Type type, ParameterAttributes paramAttrs = default(ParameterAttributes), IEnumerable<AttributeInfo> customAttrs = null) {
			NameImpl = name ?? "";
			ClassImpl = type ?? throw new ArgumentNullException(nameof(type));
			AttrsImpl = paramAttrs;
			
			_attributeInfo = new LinkedList<AttributeInfo>
				( customAttrs ?? new AttributeInfo[0] );
		}

		public new ref string Name => ref NameImpl;

		public new ref Type ParameterType => ref ClassImpl;

		public new ref ParameterAttributes Attributes => ref AttrsImpl;
		
		public new ref int Position => ref PositionImpl;

		public int GetPosition() => PositionImpl;

		public CustomParameterInfo(Type type, ParameterAttributes paramAttrs = default(ParameterAttributes), IEnumerable<AttributeInfo> customAttrs = null)
			: this(null, type, paramAttrs, customAttrs) {}

		public CustomParameterInfo(Type type, ParameterAttributes paramAttrs, params AttributeInfo[] customAttrs)
			: this(null, type, paramAttrs, customAttrs) {}
		
		public CustomParameterInfo(Type type, IEnumerable<AttributeInfo> customAttrs = null)
			: this(null, type, default(ParameterAttributes), customAttrs) {}

		
		public CustomParameterInfo(Type type, params AttributeInfo[] customAttrs)
			: this(null, type, customAttrs) {}
		
		public CustomParameterInfo(string name, Type type, IEnumerable<AttributeInfo> customAttrs)
			: this(name, type, default(ParameterAttributes), customAttrs) {}
		
		public CustomParameterInfo(string name, Type type, params AttributeInfo[] customAttrs)
			: this(name, type, (IEnumerable<AttributeInfo>)customAttrs) {}
		
		private readonly ICollection<AttributeInfo> _attributeInfo;

		public IEnumerable<AttributeInfo> AttributeInfos
			=> _attributeInfo;
		
		public void AddCustomAttribute(AttributeInfo attribute)
			=> _attributeInfo.Add(attribute);
		
		public AttributeInfo AddCustomAttribute<TAttribute>(Expression<Func<TAttribute>> funcExpr) {
			var attributeInfo = AttributeInfo.Create(funcExpr);
			_attributeInfo.Add(attributeInfo);
			return attributeInfo;
		}

		public bool RemoveCustomAttribute(AttributeInfo attribute)
			=> _attributeInfo.Remove(attribute);

		public override object[] GetCustomAttributes(bool inherit)
			=> _attributeInfo.Select(ai => ai.GetAttribute()).ToArray<object>();

		public override object[] GetCustomAttributes(Type attributeType, bool inherit)
			=> _attributeInfo.Where(ai => ai.Type == attributeType)
				.Select(ai => ai.GetAttribute()).ToArray<object>();

		public override IList<CustomAttributeData> GetCustomAttributesData()
			=> CustomAttributes.ToArray();

		public override IEnumerable<CustomAttributeData> CustomAttributes
			=> _attributeInfo.Select(ai => ai.GetCustomAttributeData());
	}
}