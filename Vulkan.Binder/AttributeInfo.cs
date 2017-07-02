using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Vulkan.Binder.Extensions;
using CecilCustomAttribute = Mono.Cecil.CustomAttribute;
using CecilCustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;

namespace Vulkan.Binder {
	public sealed class AttributeInfo {
		public static AttributeInfo Create<TAttribute>(Expression<Func<TAttribute>> funcExpr) {
			NewExpression newExpr;
			IDictionary<FieldInfo, object> fields
				= ImmutableDictionary<FieldInfo, object>.Empty;
			IDictionary<PropertyInfo, object> props
				= ImmutableDictionary<PropertyInfo, object>.Empty;
			if (funcExpr.Body is MemberInitExpression initExpr) {
				newExpr = initExpr.NewExpression;
				fields = new Dictionary<FieldInfo, object>();
				props = new Dictionary<PropertyInfo, object>();
				foreach (var binding in initExpr.Bindings) {
					if (binding.BindingType == MemberBindingType.Assignment) {
						if (binding.Member is FieldInfo field) {
							fields.Add(field, ReflectionHelpers.ResolveConst(binding));
						}
						else if (binding.Member is PropertyInfo prop) {
							props.Add(prop, ReflectionHelpers.ResolveConst(binding));
						}
						else
							throw new NotImplementedException();
					}
					else
						throw new NotImplementedException();
				}
			}
			else if (funcExpr.Body is NewExpression newExprBody) {
				newExpr = newExprBody;
			}
			else
				throw new NotSupportedException();

			var attrType = typeof(TAttribute);
			if (attrType != newExpr.Type)
				throw new NotSupportedException();
			ICollection<object> args
				= new LinkedList<object>();

			for (var i = 0 ; i < newExpr.Arguments.Count ; i++) {
				var arg = newExpr.Arguments[i];
				args.Add(ReflectionHelpers.ResolveConst(arg));
			}

			return new AttributeInfo(newExpr.Constructor, args.ToArray(),
				props.Keys.ToArray(), props.Values.ToArray(),
				fields.Keys.ToArray(), fields.Values.ToArray());
		}

		public AttributeInfo(ConstructorInfo constructor, object[] arguments = null, PropertyInfo[] propertiesInitialized = null, object[] propertyValues = null, FieldInfo[] fieldsInitialized = null, object[] fieldValues = null) {
			Constructor = constructor;
			Arguments = arguments ?? new object[0];
			PropertiesInitialized = propertiesInitialized ?? new PropertyInfo[0];
			PropertyValues = propertyValues ?? new object[0];
			FieldsInitialized = fieldsInitialized ?? new FieldInfo[0];
			FieldValues = fieldValues ?? new object[0];
		}

		public ConstructorInfo Constructor { get; set; }
		public object[] Arguments { get; set; }
		public PropertyInfo[] PropertiesInitialized { get; set; }
		public object[] PropertyValues { get; set; }
		public FieldInfo[] FieldsInitialized { get; set; }
		public object[] FieldValues { get; set; }

		public Type Type => Constructor.DeclaringType;

		private readonly ConditionalWeakTable<ModuleDefinition, CecilCustomAttribute> _ccaCacheTable
			= new ConditionalWeakTable<ModuleDefinition, CecilCustomAttribute>();

		public CecilCustomAttribute GetCecilCustomAttribute(ModuleDefinition module) {
			lock (_ccaCacheTable) {
				if (_ccaCacheTable.TryGetValue(module, out var cca))
					return cca;

				cca = CreateCecilCustomAttribute(module);
				_ccaCacheTable.Add(module, cca);
				return cca;
			}
		}

		private CecilCustomAttribute CreateCecilCustomAttribute(ModuleDefinition module) {
			var ctorRef = Constructor.Import(module);
			var cca = new CecilCustomAttribute(ctorRef);
			foreach (var value in Arguments)
				cca.ConstructorArguments.Add(CreateCecilTypedArgument(
					module, value));

			for (var i = 0 ; i < PropertiesInitialized.Length ; ++i) {
				var clrProp = PropertiesInitialized[i];
				var value = PropertyValues[i];
				cca.Properties.Add(CreateCecilNamedArgument(
					clrProp.Name, module, value));
			}

			for (var i = 0 ; i < FieldsInitialized.Length ; ++i) {
				var clrField = FieldsInitialized[i];
				var value = FieldValues[i];
				cca.Fields.Add(CreateCecilNamedArgument(
					clrField.Name, module, value));
			}
			return cca;
		}

		private static object MaybeImport(object value, ModuleDefinition module) {
			if (value is Type type) {
				value = type.Import(module);
			}
			else if (value is MemberInfo) {
				throw new NotImplementedException();
			}
			return value;
		}

		private static CustomAttributeArgument CreateCecilTypedArgument(ModuleDefinition module, object value)
			=> new CustomAttributeArgument(value.GetType().Import(module), MaybeImport(value, module));

		private static CecilCustomAttributeNamedArgument CreateCecilNamedArgument(string name, ModuleDefinition module, object value)
			=> new CecilCustomAttributeNamedArgument(name, CreateCecilTypedArgument(module, value));

		private Func<Attribute> _factory;

		public Func<Attribute> GetFactory()
			=> _factory ?? (_factory = CreateFactory());

		private Func<Attribute> CreateFactory() {
			var p = Constructor.GetParameters();
			return Expression.Lambda<Func<Attribute>>(
				Expression.MemberInit(
					Expression.New(
						Constructor,
						Arguments.Select((a, i) =>
							Expression.Constant(a, p[i].ParameterType)
						)
					),
					PropertiesInitialized.Select((pi, i) =>
							(MemberBinding) Expression.Bind(pi,
								Expression.Constant(PropertyValues[i], pi.PropertyType)))
						.Concat(FieldsInitialized.Select((pi, i) =>
							(MemberBinding) Expression.Bind(pi,
								Expression.Constant(FieldValues[i], pi.FieldType))))
						.ToArray())).Compile();
		}

		public static implicit operator Func<Attribute>(AttributeInfo info)
			=> info.GetFactory();

		private Attribute _attrCache;

		public Attribute GetAttribute() {
			return _attrCache ?? (_attrCache = GetFactory()());
		}

		public static implicit operator Attribute(AttributeInfo info)
			=> info.GetAttribute();

		public AttributeInfo Clone()
			=> new AttributeInfo(
				Constructor, Arguments,
				PropertiesInitialized, PropertyValues,
				FieldsInitialized, FieldValues
			);
	}
}