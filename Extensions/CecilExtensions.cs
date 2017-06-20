using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using PropertyAttributes = Mono.Cecil.PropertyAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Artilect.Vulkan.Binder.Extensions {
	public static class CecilExtensions {
		private static readonly TypeAttributes StructTypeAttributeMask = TypeAttributes.ExplicitLayout | TypeAttributes.SequentialLayout;

		public static TypeDefinition DefineType(this ModuleDefinition module, string name, TypeAttributes typeAttrs, Type baseType = null, int packing = -1, int size = -1) {
			var td = baseType != null
				? new TypeDefinition(null, name, typeAttrs, module.ImportReference(baseType))
				: new TypeDefinition(null, name, typeAttrs);
			if (packing >= 0)
				td.PackingSize = (short) packing;
			if (packing >= 0)
				td.ClassSize = size;
			if ((typeAttrs & StructTypeAttributeMask) != 0) {
				td.BaseType = typeof(ValueType).Import(module);
				td.Attributes |= TypeAttributes.Serializable;
			}
			td.Scope = module;
			module.Types.Add(td);
			return td;
		}

		public static TypeDefinition DefineEnum(this ModuleDefinition module, string name, TypeAttributes typeAttrs, TypeReference underlyingType = null) {
			var td = new TypeDefinition(null, name, typeAttrs | TypeAttributes.Sealed | TypeAttributes.Serializable, module.ImportReference(typeof(Enum)));
			if (underlyingType == null) {
				underlyingType = typeof(int).Import(module);
			}
			var enumField = new FieldDefinition("value__",
				FieldAttributes.Public
				| FieldAttributes.SpecialName
				| FieldAttributes.RTSpecialName, underlyingType);
				td.Fields.Add(enumField);
			td.Scope = module;
			module.Types.Add(td);
			return td;
		}

		public static void ChangeUnderlyingType(this TypeDefinition td, TypeReference underlyingType) {
			if (!td.IsEnum) 
				throw new NotImplementedException();
			var valueField = td.Fields.First(fd => !fd.IsStatic && fd.Name == "value__");
			valueField.FieldType = underlyingType;
			if ( td.Fields.Any(fd => fd.IsLiteral) )
				throw new NotImplementedException();
			/*
			foreach (var fd in td.Fields.Where(fd => fd.IsLiteral)) {
				fd.Constant = Convert.ChangeType()
			}
			*/
		}
		
		public static TypeReference GetUnderlyingType(this TypeDefinition td) {
			if (!td.IsEnum) 
				throw new NotImplementedException();
			var valueField = td.Fields.First(fd => !fd.IsStatic && fd.Name == "value__");
			return valueField.FieldType;
		}

		public static TypeDefinition DefineEnum(this ModuleDefinition module, string name, TypeAttributes typeAttrs, Type underlyingType = null) {
			return DefineEnum(module, name, typeAttrs, underlyingType.Import(module));
		}

		public static TypeDefinition DefineEnum(this ModuleDefinition module, string name, TypeAttributes typeAttrs) {
			return DefineEnum(module, name, typeAttrs, typeof(int).Import(module));
		}

		public static FieldDefinition DefineLiteral(this TypeDefinition typeDef, string name, object value) {
			var fd = new FieldDefinition(name,
				FieldAttributes.Public
				| FieldAttributes.HasDefault
				| FieldAttributes.Static
				| FieldAttributes.Literal, typeDef);
			
			typeDef.Fields.Add(fd);
			fd.Constant = value;
			//var bytes = value as byte[] ?? BitConverter.GetBytes((dynamic) value);
			//fd.InitialValue = bytes;
			return fd;
		}

		public static FieldDefinition DefineField(this TypeDefinition typeDef, string name, TypeReference fieldType, FieldAttributes fieldAttrs) {
			var fd = new FieldDefinition(name, fieldAttrs, fieldType);
			typeDef.Fields.Add(fd);
			return fd;
		}

		public static void SetCustomAttribute(this FieldDefinition fieldDef, AttributeInfo attrInfo, ModuleDefinition module) {
			fieldDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(module));
		}
		
		public static IEnumerable<TypeReference> GetInterfaces(this TypeDefinition typeDef) {
			return typeDef.Interfaces.Select(ii => ii.InterfaceType);
		}
		public static IEnumerable<TypeReference> GetInterfaces(this TypeReference typeRef) {
			return typeRef.ResolveDefinition().GetInterfaces();
		}

		public static TypeDefinition ResolveDefinition(this TypeReference typeRef) {;
			if (typeRef is TypeDefinition typeDef)
				return typeDef;
			if (typeRef.IsIndirect())
				throw new InvalidOperationException();
			typeDef = typeRef.Resolve();
			return typeDef;
		}

		public static IEnumerable<TypeReference> Import(this ModuleDefinition module, IEnumerable<Type> types) {
			foreach (var type in types)
				yield return module.ImportReference(type);
		}

		public static IEnumerable<TypeReference> Import(this IEnumerable<Type> types, ModuleDefinition module) {
			foreach (var type in types)
				yield return module.ImportReference(type);
		}

		public static MethodDefinition DefineConstructor(this TypeDefinition typeDef, MethodAttributes methodAttrs, params Type[] paramTypes) {
			var module = typeDef.Module;
			var isStatic = methodAttrs.HasFlag(MethodAttributes.Static);
			var methodDef = new MethodDefinition(isStatic ? ".cctor" : ".ctor", methodAttrs,
				module.ImportReference(typeof(void)));
			var pds = module.Import(paramTypes)
				.Select(typeRef => new ParameterDefinition(typeRef));
			foreach (var pd in pds)
				methodDef.Parameters.Add(pd);
			typeDef.Methods.Add(methodDef);
			return methodDef;
		}

		public static void SetImplementationFlags(this MethodDefinition methodDef, MethodImplAttributes attrs) {
			methodDef.ImplAttributes = attrs;
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, TypeReference retType, params Type[] paramTypes) {
			return DefineMethod(typeDef, name, methodAttrs, retType, paramTypes.Import(typeDef.Module));
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, TypeReference retType, params TypeReference[] paramTypes) {
			return DefineMethod(typeDef, name, methodAttrs, retType, (IEnumerable<TypeReference>) paramTypes);
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, TypeReference retType, IEnumerable<TypeReference> paramTypes) {
			var module = typeDef.Module;
			var methodDef = new MethodDefinition(name, methodAttrs, retType);
			var pds = paramTypes
				.Select(typeRef => new ParameterDefinition(typeRef));
			foreach (var pd in pds)
				methodDef.Parameters.Add(pd);
			typeDef.Methods.Add(methodDef);
			return methodDef;
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, Type retType, params Type[] paramTypes) {
			var module = typeDef.Module;
			var methodDef = new MethodDefinition(name, methodAttrs,
				module.ImportReference(retType));
			var pds = CreateParametersDefinitions(module, paramTypes);
			foreach (var pd in pds)
				methodDef.Parameters.Add(pd);
			typeDef.Methods.Add(methodDef);
			return methodDef;
		}

		private static IEnumerable<ParameterDefinition> CreateParametersDefinitions(ModuleDefinition module, params Type[] paramTypes)
			=> CreateParametersDefinitions(paramTypes.Import(module).ToArray());

		private static IEnumerable<ParameterDefinition> CreateParametersDefinitions(params TypeReference[] paramTypes)
			=> CreateParametersDefinitions((IEnumerable<TypeReference>) paramTypes);

		private static IEnumerable<ParameterDefinition> CreateParametersDefinitions(IEnumerable<TypeReference> paramTypes)
			=> paramTypes?.Select(typeRef => new ParameterDefinition(typeRef));

		public static void SetCustomAttribute<T>(this TypeDefinition typeDef, Expression<Func<T>> expr) {
			typeDef.SetCustomAttribute(AttributeInfo.Create(expr));
		}

		public static void SetCustomAttribute(this TypeDefinition typeDef, AttributeInfo attrInfo)
			=> typeDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(typeDef.Module));
		
		public static void SetCustomAttribute<T>(this MethodDefinition methodDef, Expression<Func<T>> expr) {
			methodDef.SetCustomAttribute(AttributeInfo.Create(expr));
		}

		public static void SetCustomAttribute(this MethodDefinition methodDef, AttributeInfo attrInfo)
			=> methodDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(methodDef.Module));

		public static void AddInterfaceImplementation(this TypeDefinition typeDef, TypeReference interfaceType)
			=> typeDef.Interfaces.Add(new InterfaceImplementation(interfaceType));

		public static void AddInterfaceImplementation(this TypeDefinition typeDef, Type interfaceType)
			=> typeDef.AddInterfaceImplementation(typeDef.Module.ImportReference(interfaceType));

		public static ParameterDefinition DefineParameter(this MethodDefinition methodDef, int position, ParameterAttributes paramAttrs, string name) {
			if (position == 0)
				throw new NotImplementedException();
			var param = methodDef.Parameters[position - 1];
			param.Attributes = paramAttrs;
			param.Name = name;
			return param;
		}

		public static ParameterDefinition DefineParameter(this MethodDefinition methodDef, int position, System.Reflection.ParameterAttributes paramAttrs, string name)
			=> DefineParameter(methodDef, position, (ParameterAttributes) paramAttrs, name);

		public static void SetCustomAttribute(this ParameterDefinition paramDef, AttributeInfo attrInfo)
			=> paramDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(((MethodDefinition) paramDef.Method).Module));

		public static TypeDefinition CreateType(this TypeDefinition td) => td;

		public static bool IsCreated(this TypeDefinition td) => true;

		public static GenericInstanceType MakeGenericType(this Type type, params TypeReference[] typeParams) {
			var module = typeParams.First().Module;
			var typeRef = module.ImportReference(type);
			return typeRef.MakeGenericInstanceType(typeParams);
		}

		public static PropertyDefinition DefineProperty(this TypeDefinition typeDef, string name, PropertyAttributes propAttrs, TypeReference propType, params TypeReference[] paramTypes) {
			var module = typeDef.Module;
			var propDef = new PropertyDefinition(name, propAttrs, module.ImportReference(propType));
			var paramDefs = CreateParametersDefinitions(paramTypes);
			foreach (var paramDef in paramDefs)
				propDef.Parameters.Add(paramDef);
			typeDef.Properties.Add(propDef);
			return propDef;
		}

		public static PropertyDefinition DefineProperty(this TypeDefinition typeDef, string name, PropertyAttributes propAttrs, TypeReference propType, params Type[] paramTypes) {
			var module = typeDef.Module;
			var propDef = new PropertyDefinition(name, propAttrs, module.ImportReference(propType));
			var paramDefs = CreateParametersDefinitions(module, paramTypes);
			foreach (var paramDef in paramDefs)
				propDef.Parameters.Add(paramDef);
			typeDef.Properties.Add(propDef);
			return propDef;
		}

		public static PropertyDefinition DefineProperty(this TypeDefinition typeDef, string name, PropertyAttributes propAttrs, Type propType, params Type[] paramTypes) {
			var module = typeDef.Module;
			var propDef = new PropertyDefinition(name, propAttrs, module.ImportReference(propType));
			var paramDefs = CreateParametersDefinitions(module, paramTypes);
			foreach (var paramDef in paramDefs)
				propDef.Parameters.Add(paramDef);
			typeDef.Properties.Add(propDef);
			return propDef;
		}

		public static void SetGetMethod(this PropertyDefinition propDef, MethodDefinition methodDef)
			=> propDef.GetMethod = methodDef;

		public static bool Contains(this Collection<InterfaceImplementation> interfaces, TypeReference interfaceType)
			=> interfaces.Select(ii => ii.InterfaceType).Contains(interfaceType);

		private const BindingFlags AnyAccessInstanceOrStaticBindingFlags
			= BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

		public static MethodReference GetMethod(this TypeDefinition typeDef, string methodName, BindingFlags bf) {
			if ((AnyAccessInstanceOrStaticBindingFlags & bf) == AnyAccessInstanceOrStaticBindingFlags)
				return typeDef.Methods.Single(md => md.Name == methodName);
			return typeDef.Methods.Single
			(md => md.Name == methodName
					&& (
						bf.HasFlag(BindingFlags.Public) && md.IsPublic
						|| bf.HasFlag(BindingFlags.NonPublic) && !md.IsPublic
					)
					&& (
						bf.HasFlag(BindingFlags.Static) && md.IsStatic
						|| bf.HasFlag(BindingFlags.Instance) && !md.IsStatic
					));
		}

		public static TypeReference Import(this Type type, ModuleDefinition module)
			=> module.GetType(type.FullName) ?? module.ImportReference(type);

		public static Type GetRuntimeType(this TypeReference type)
			=> Type.GetType(type.FullName, false)
				?? throw new NotSupportedException();

		public static Type ExpressionOnly(this TypeReference type)
			=> throw new NotSupportedException("Expressions processing should filter out this call.");


		public static bool IsDirect(this TypeReference type)
			=> !type.IsIndirect();

		public static bool IsIndirect(this TypeReference type)
			=> type.IsArray || type.IsPointer || type.IsByReference;

		public static bool IsDirectOrArray(this TypeReference type)
			=> !(type.IsPointer || type.IsByReference);


		public static int SizeOf(this TypeReference type)
			=> type.IsPointer || type.IsByReference
				? IntPtr.Size
				: type.ResolveDefinition().ClassSize;

		public static int SizeOf(this TypeDefinition type)
			=> type.ClassSize;

		public static int UnsafeSizeOf(this TypeReference type)
			=> type.SizeOf();

		public static int UnsafeSizeOf(this TypeDefinition type)
			=> type.SizeOf();

		public static void DefineMethodOverride(this TypeDefinition typeDef, MethodDefinition overrider, MethodReference overridden) {
			overrider.Overrides.Add(overridden);
		}

		public static PropertyReference GetProperty(this TypeDefinition typeDef, string propName) {
			return typeDef.Properties.First(prop => prop.Name == propName);
		}


		public static PropertyReference GetProperty(this TypeDefinition typeDef, string propName, BindingFlags bf) {
			if ((AnyAccessInstanceOrStaticBindingFlags & bf) == AnyAccessInstanceOrStaticBindingFlags)
				return typeDef.Properties.Single(md => md.Name == propName);
			return typeDef.Properties.Single
			(md => md.Name == propName
					&& (
						bf.HasFlag(BindingFlags.Public)
						&& (md.GetMethod?.IsPublic ?? md.SetMethod?.IsPublic ?? false)
						|| bf.HasFlag(BindingFlags.NonPublic)
						&& !(md.GetMethod?.IsPublic ?? md.SetMethod?.IsPublic ?? false)
					)
					&& (
						bf.HasFlag(BindingFlags.Static)
						&& (md.GetMethod?.IsStatic ?? md.SetMethod?.IsStatic ?? false)
						|| bf.HasFlag(BindingFlags.Instance)
						&& !(md.GetMethod?.IsStatic ?? md.SetMethod?.IsStatic ?? false)
					));
		}

		public static bool Contains(this IEnumerable<TypeReference> typeRefs, TypeReference otherRef) {
			var orMdt = otherRef.MetadataToken.ToUInt32();
			var orScope = otherRef.Scope;
			var orIsValueType = otherRef.IsValueType;
			var orIsPrimitive = otherRef.IsPrimitive;
			var orIsPointer = otherRef.IsPointer;
			var orIsByReference = otherRef.IsByReference;
			var orIsArray = otherRef.IsArray;
			var orIsDirect = !(orIsPointer || orIsByReference || orIsArray);
			var otherGit = otherRef as GenericInstanceType;
			TypeDefinition otherResolved = null;
			foreach (var typeRef in typeRefs) {
				var trMdt = typeRef.MetadataToken.ToUInt32();
				if (trMdt == orMdt && typeRef.Scope == orScope && trMdt != 0x01000000 )
					return true;
				if (trMdt != 0x01000000 && typeRef.Scope == otherRef.Scope)
					continue;
				if (typeRef.IsValueType != orIsValueType || typeRef.IsPrimitive != orIsPrimitive)
					continue;
				if (typeRef is GenericInstanceType typeGit && otherGit != null) {
					if (typeRef.Resolve() == otherGit.Resolve()
						&& typeGit.GenericParameters.SequenceEqual(otherGit.GenericParameters))
						return true;
					continue;
				}
				if (typeRef.IsDirect() && orIsDirect || typeRef.IsArray && orIsArray) {
					if ( otherResolved == null )
						otherResolved = otherRef.Resolve();
					if (typeRef.Resolve() == otherResolved)
						return true;
					continue;
				}
				if (typeRef.IsPointer && orIsPointer
					|| typeRef.IsByReference && orIsByReference
					|| typeRef.IsArray && orIsArray) {
					if (typeRef.DescendElementType().Is(otherRef.DescendElementType()))
						return true;
					// continue;
				}
				// continue;
			}
			return false;
		}

		public static bool Is(this TypeReference typeRef, TypeReference otherRef) {
			var trMdt = typeRef.MetadataToken.ToUInt32();
			var orMdt = otherRef.MetadataToken.ToUInt32();
			if (trMdt == orMdt && typeRef.Scope == otherRef.Scope && trMdt != 0x01000000 )
				return true;
			if (trMdt != 0x01000000 && typeRef.Scope == otherRef.Scope)
				return false;
			if (typeRef.IsValueType != otherRef.IsValueType || typeRef.IsPrimitive != otherRef.IsPrimitive)
				return false;
			if (typeRef is GenericInstanceType typeGit && otherRef is GenericInstanceType otherGit)
				return typeRef.Resolve() == otherRef.Resolve() && typeRef.GenericParameters.SequenceEqual(otherGit.GenericParameters);
			if (typeRef.IsDirect() && otherRef.IsDirect() || typeRef.IsArray && otherRef.IsArray)
				return typeRef.Resolve() == otherRef.Resolve();
			if (typeRef.IsPointer && otherRef.IsPointer
				|| typeRef.IsByReference && otherRef.IsByReference
				|| typeRef.IsArray && otherRef.IsArray)
				return typeRef.DescendElementType().Is(otherRef.DescendElementType());
			return false;
		}

		public static bool IsAssignableFrom(this TypeReference typeRef, TypeReference otherRef) {
			for (; ;) {
				if (otherRef == null)
					return false;

				if (typeRef == otherRef)
					return true;

				if (typeRef.IsValueType && !otherRef.IsValueType)
					return false;

				if (typeRef.IsFunctionPointer != otherRef.IsFunctionPointer)
					return false;

				if (typeRef.IsByReference != otherRef.IsByReference)
					return false;

				if (typeRef.IsPointer != otherRef.IsPointer)
					return false;

				if (typeRef.IsArray != otherRef.IsArray)
					return false;

				if (typeRef.IsByReference) {
					do {
						typeRef = typeRef.DescendElementType();
						otherRef = otherRef.DescendElementType();
					} while (typeRef.IsByReference && otherRef.IsByReference);
					continue;
				}

				if (typeRef.IsPointer) {
					do {
						typeRef = typeRef.DescendElementType();
						otherRef = otherRef.DescendElementType();
					} while (typeRef.IsPointer && otherRef.IsPointer);
					continue;
				}

				if (typeRef.IsArray) {
					do {
						typeRef = typeRef.DescendElementType();
						otherRef = otherRef.DescendElementType();
					} while (typeRef.IsArray && otherRef.IsArray);
					continue;
				}
				var typeDef = typeRef.ResolveDefinition();
				if (typeDef != null) {
					var otherDef = otherRef.ResolveDefinition();

					if (typeDef.IsInterface
						&& otherDef.Interfaces.Contains(typeDef))
						return true;

					otherRef = otherDef.BaseType?.Resolve();
					continue;
				}

				throw new NotImplementedException();
			}
		}

		public delegate void TypeReferenceTransform(ref TypeReference t);

		public static void MakeArrayType(ref TypeReference t) {
			var s = t.Scope;
			var m = t.Module;
			t = t.MakeArrayType();
			t = m.ImportReference(t);
		}

		public static void MakeByRefType(ref TypeReference t) {
			var s = t.Scope;
			var m = t.Module;
			t = t.MakeByReferenceType();
			m.ImportReference(t);
			t = m.ImportReference(t);
		}

		public static void MakePointerType(ref TypeReference t) {
			var s = t.Scope;
			var m = t.Module;
			t = t.MakePointerType();
			t = m.ImportReference(t);
		}

		public static TypeReference GetTypePointedTo(this ModuleDefinition module, Type exterior, out LinkedList<TypeReferenceTransform> transform, bool directVoids = false)
			=> GetTypePointedTo(exterior.Import(module), out transform, directVoids);

		public static TypeReference GetTypePointedTo(this TypeReference exterior, out LinkedList<TypeReferenceTransform> transform, bool directVoids = false) {
			var type = exterior;
			transform = new LinkedList<TypeReferenceTransform>();
			while (type.IsPointer) {
				if (directVoids || type != typeof(void).Import(exterior.Module)) {
					transform.AddFirst(MakePointerType);
					type = type.DescendElementType();
				}
				else
					return type;
			}
			return type;
		}

		public static TypeReference DescendElementType(this TypeReference typeRef) {
			if (typeRef is TypeSpecification typeSpec)
				return typeSpec.ElementType;
			throw new NotImplementedException();
		}

		public static TypeReference GetInteriorType(this TypeReference exterior, out LinkedList<TypeReferenceTransform> transform, bool directVoids = false) {
			transform = new LinkedList<TypeReferenceTransform>();
			return FindInteriorType(exterior, ref transform, directVoids);
		}

		public static TypeReference FindInteriorType(this TypeReference exterior, ref LinkedList<TypeReferenceTransform> transform, bool directVoids = false) {
			var type = exterior;
			for (; ;) {
				if (type.IsPointer) {
					if (directVoids || type != typeof(void).Import(exterior.Module)) {
						transform.AddFirst(MakePointerType);
						type = (type as PointerType ?? throw new NotImplementedException()).ElementType;
					}
					else {
						break;
					}
				}
				// type.MetadataType == MetadataType.Array
				else if (type.IsArray) {
					transform.AddFirst(MakeArrayType);
					type = (type as ArrayType ?? throw new NotImplementedException()).ElementType;
				}

				// type.MetadataType == MetadataType.ByReference
				// type.MetadataType == MetadataType.TypedByReference
				else if (type.IsByReference) {
					transform.AddFirst(MakeByRefType);
					type = (type as ByReferenceType ?? throw new NotImplementedException()).ElementType;
				}
				else
					break;
			}
			return type;
		}
	}
}