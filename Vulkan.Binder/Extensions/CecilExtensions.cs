using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using PropertyAttributes = Mono.Cecil.PropertyAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Vulkan.Binder.Extensions {
	public static class CecilExtensions {

		private const TypeAttributes StructTypeAttributeMask
			= TypeAttributes.ExplicitLayout
			| TypeAttributes.SequentialLayout;

		public static TypeDefinition DefineType(this ModuleDefinition module, string name, TypeAttributes typeAttrs, TypeReference baseType = null, int packing = -1, int size = -1) {
			var td = baseType != null
				? new TypeDefinition(null, name, typeAttrs, baseType)
				: new TypeDefinition(null, name, typeAttrs);
			if (packing > 0)
				td.PackingSize = (short) packing;
			if (packing > 0)
				td.ClassSize = size;
			if (baseType == null) {
				if ((typeAttrs & StructTypeAttributeMask) != 0) {
					if (packing <= 0)
						td.PackingSize = 1;
					if (size <= 0)
						td.ClassSize = 1;
					td.BaseType = module.TypeSystem.ValueType;
					td.Attributes |= TypeAttributes.Serializable | TypeAttributes.BeforeFieldInit;
					td.IsValueType = true;
				}
				if (td.BaseType == null) {
					if ((typeAttrs & TypeAttributes.Interface) != 0)
						td.BaseType = null;
					else
						td.BaseType = module.TypeSystem.Object;
				}
			}
			else {
				if ((typeAttrs & TypeAttributes.Interface) != 0)
					td.BaseType = module.TypeSystem.Object;
				else
					td.BaseType = baseType.Import(module);
			}

			//td.Scope = module;
			module.Types.Add(td);
			return td;
		}

		public static TypeDefinition DefineEnum(this ModuleDefinition module, string name, TypeAttributes typeAttrs, TypeReference underlyingType) {
			var td = new TypeDefinition(null, name,
				typeAttrs
				| TypeAttributes.Sealed
				| TypeAttributes.Serializable,
				module.TypeSystem.Enum);
			if (underlyingType == null) {
				underlyingType = module.TypeSystem.Int32;
			}
			var enumField = new FieldDefinition("value__",
				FieldAttributes.Public
				| FieldAttributes.SpecialName
				| FieldAttributes.RTSpecialName, underlyingType.Import(module));
			td.Fields.Add(enumField);

			//td.Scope = module;
			module.Types.Add(td);
			return td;
		}

		public static void ChangeUnderlyingType(this TypeDefinition td, TypeReference underlyingType) {
			if (!td.IsEnum)
				throw new NotImplementedException();

			var valueField = td.Fields.First(fd => !fd.IsStatic && fd.Name == "value__");
			valueField.FieldType = underlyingType.Import(td.Module);
			if (td.Fields.Any(fd => fd.IsLiteral))
				throw new NotImplementedException();
			/*
			foreach (var fd in td.Fields.Where(fd => fd.IsLiteral)) {
				fd.Constant = Convert.ChangeType()
			}
			*/
		}

		public static TypeReference Require(this TypeReference tr, bool nothrow = false) {
			return
				tr.Resolve() != null
					? tr
					: nothrow
						? (TypeReference) null
						: throw new TypeLoadException
							($"Could not resolve type reference to {tr.FullName}.");
		}

		public static TypeReference GetUnderlyingType(this TypeDefinition td) {
			if (!td.IsEnum)
				throw new NotImplementedException();

			var valueField = td.Fields.First(fd => !fd.IsStatic && fd.Name == "value__");
			return valueField.FieldType;
		}

		public static TypeDefinition DefineEnum(this ModuleDefinition module, string name, TypeAttributes typeAttrs, Type underlyingType)
			=> DefineEnum(module, name, typeAttrs, underlyingType.Import(module));

		public static TypeDefinition DefineEnum(this ModuleDefinition module, string name, TypeAttributes typeAttrs)
			=> DefineEnum(module, name, typeAttrs, module.TypeSystem.Int32);

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
			if (((fieldType as ByReferenceType)?.ElementType as ByReferenceType) != null)
				throw new NotImplementedException();

			var typeRef = fieldType.Import(typeDef.Module);
			var fd = new FieldDefinition(name, fieldAttrs, typeRef);
			typeDef.Fields.Add(fd);
			return fd;
		}

		public static void SetCustomAttribute(this FieldDefinition fieldDef, AttributeInfo attrInfo, ModuleDefinition module) {
			fieldDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(module));
		}

		public static IEnumerable<TypeReference> GetInterfaces(this TypeDefinition typeDef)
			=> typeDef.Interfaces.Select(ii => ii.InterfaceType);

		public static IEnumerable<TypeReference> GetInterfaces(this TypeReference typeRef)
			=> typeRef.Resolve().GetInterfaces();

		public static IEnumerable<TypeReference> Import(this IEnumerable<TypeReference> types, ModuleDefinition module) {
			foreach (var type in types)
				yield return type.Import(module);
		}

		public static IEnumerable<TypeReference> Import(this IEnumerable<Type> types, ModuleDefinition module) {
			foreach (var type in types)
				yield return type.Import(module);
		}

		public static MethodDefinition DefineConstructor(this TypeDefinition typeDef, MethodAttributes methodAttrs, params TypeReference[] paramTypes) {
			var module = typeDef.Module;
			methodAttrs |= MethodAttributes.HideBySig
							| MethodAttributes.RTSpecialName
							| MethodAttributes.SpecialName;
			methodAttrs &= ~ MethodAttributes.NewSlot;
			var isStatic = methodAttrs.HasFlag(MethodAttributes.Static);
			if (isStatic) {
				methodAttrs &= ~ MethodAttributes.Public;
				methodAttrs |= MethodAttributes.Private;
			}
			var methodDef = new MethodDefinition(isStatic ? ".cctor" : ".ctor", methodAttrs,
				module.TypeSystem.Void);
			var pds = paramTypes.Import(module)
				.Select(typeRef => new ParameterDefinition(typeRef));
			foreach (var pd in pds)
				methodDef.Parameters.Add(pd);

			typeDef.Methods.Add(methodDef);
			return methodDef;
		}


		public static void SetImplementationFlags(this MethodDefinition methodDef, MethodImplAttributes attrs) {
			methodDef.ImplAttributes = attrs;
		}
		
		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, TypeReference retType, params TypeReference[] paramTypes) {
			return DefineMethod(typeDef, name, methodAttrs, retType, (IEnumerable<TypeReference>) paramTypes);
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, TypeReference retType, IEnumerable<TypeReference> paramTypes) {
			//var module = typeDef.Module;
			var methodDef = new MethodDefinition(name, methodAttrs, retType.Import(typeDef.Module));
			var pds = paramTypes.Import(typeDef.Module)
				.Select(typeRef => new ParameterDefinition(typeRef));
			foreach (var pd in pds)
				methodDef.Parameters.Add(pd);

			typeDef.Methods.Add(methodDef);
			return methodDef;
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, TypeReference retType, IEnumerable<ParameterDefinition> paramDefs) {
			//var module = typeDef.Module;
			var methodDef = new MethodDefinition(name, methodAttrs, retType.Import(typeDef.Module));
			var pds = paramDefs
				.Select(pd => new ParameterDefinition(
					pd.Name, pd.Attributes,
					pd.ParameterType.Import(typeDef.Module)));
			foreach (var pd in pds)
				methodDef.Parameters.Add(pd);

			typeDef.Methods.Add(methodDef);
			return methodDef;
		}

		public static MethodDefinition DefineMethod(this TypeDefinition typeDef, string name, MethodAttributes methodAttrs, Type retType, params Type[] paramTypes) {
			var module = typeDef.Module;
			var methodDef = new MethodDefinition(name, methodAttrs,
				retType.Import(module));
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

		public static void SetCustomAttribute<T>(this TypeDefinition typeDef, Expression<Func<T>> expr)
			=> typeDef.SetCustomAttribute(AttributeInfo.Create(expr));

		public static void SetCustomAttribute(this TypeDefinition typeDef, AttributeInfo attrInfo)
			=> typeDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(typeDef.Module));

		public static void SetCustomAttribute<T>(this MethodDefinition methodDef, Expression<Func<T>> expr)
			=> methodDef.SetCustomAttribute(AttributeInfo.Create(expr));

		public static void SetCustomAttribute(this MethodDefinition methodDef, AttributeInfo attrInfo)
			=> methodDef.CustomAttributes.Add(attrInfo.GetCecilCustomAttribute(methodDef.Module));

		public static void AddInterfaceImplementation(this TypeDefinition typeDef, TypeReference interfaceType)
			=> typeDef.Interfaces.Add(new InterfaceImplementation(interfaceType));

		public static void AddInterfaceImplementation(this TypeDefinition typeDef, Type interfaceType)
			=> typeDef.AddInterfaceImplementation(interfaceType.Import(typeDef.Module));

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

		public static TypeDefinition CreateType(this TypeDefinition td) {
			Debug.WriteLine($"Created {td.FullName}");
			return td;
		}

		public static bool IsCreated(this TypeDefinition td) {
			Debug.WriteLine($"Checking if {td.FullName} is created");
			return true;
		}

		public static GenericInstanceType MakeGenericType(this Type type, params TypeReference[] typeParams) {
			var module = typeParams.First().Module;
			var typeRef = type.Import(module);
			return typeRef.MakeGenericInstanceType(typeParams);
		}

		public static PropertyDefinition DefineProperty(this TypeDefinition typeDef, string name, PropertyAttributes propAttrs, TypeReference propType, params TypeReference[] paramTypes) {
			if (((propType as ByReferenceType)?.ElementType as ByReferenceType) != null)
				throw new NotImplementedException();

			var module = typeDef.Module;
			var propDef = new PropertyDefinition(name, propAttrs, propType.Import(module));
			var paramDefs = CreateParametersDefinitions(paramTypes);
			foreach (var paramDef in paramDefs)
				propDef.Parameters.Add(paramDef);

			typeDef.Properties.Add(propDef);
			return propDef;
		}

		public static PropertyDefinition DefineProperty(this TypeDefinition typeDef, string name, PropertyAttributes propAttrs, Type propType, params Type[] paramTypes) {
			var module = typeDef.Module;
			var propDef = new PropertyDefinition(name, propAttrs, propType.Import(module));
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

		public static MethodReference GetMethod(this TypeDefinition typeDef, string methodName, BindingFlags bf = AnyAccessInstanceOrStaticBindingFlags) {
			if ((AnyAccessInstanceOrStaticBindingFlags & bf) == AnyAccessInstanceOrStaticBindingFlags)
				return typeDef.Methods.SingleOrDefault(md => md.Name == methodName);

			return typeDef.Methods.SingleOrDefault
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

		public delegate void TypeIndirectionTransform(ref Type t);

		public static void TypeMakePointer(ref Type t) => t = t.MakePointerType();

		public static void TypeMakeArray(ref Type t) => t = t.MakeArrayType();

		public static void TypeMakeByRef(ref Type t) => t = t.MakeByRefType();

		public static Type GetInteriorType(this Type t, out IEnumerable<TypeIndirectionTransform> transforms) {
			var transformsCollection = new LinkedList<TypeIndirectionTransform>();
			transforms = transformsCollection;
			do {
				if (t.IsPointer) {
					transformsCollection.AddLast(TypeMakePointer);
				}
				else if (t.IsArray) {
					transformsCollection.AddLast(TypeMakeArray);
				}
				else if (t.IsByRef) {
					transformsCollection.AddLast(TypeMakeByRef);
				}
				else
					break;

				t = t.GetElementType();
			} while (t.HasElementType);

			return t;
		}

		public static IEnumerable<TypeReferenceTransform> ConvertTransforms(this IEnumerable<TypeIndirectionTransform> transforms) {
			foreach (var transform in transforms) {
				if (transform == TypeMakePointer)
					yield return MakePointerType;
				else if (transform == TypeMakeArray)
					yield return MakeArrayType;
				else if (transform == TypeMakeByRef)
					yield return MakeByRefType;
				else
					throw new NotImplementedException();
			}
		}

		public static TypeReference ApplyTransforms(this TypeReference typeRef, IEnumerable<TypeIndirectionTransform> transforms) {
			return typeRef?.ApplyTransforms(transforms?.ConvertTransforms());
		}

		public static TypeReference ApplyTransforms(this TypeReference typeRef, IEnumerable<TypeReferenceTransform> transforms) {
			if (typeRef == null)
				return null;
			if (transforms == null)
				return typeRef;

			foreach (var transform in transforms)
				transform(ref typeRef);

			return typeRef;
		}

		public static MethodReference Import(this MethodInfo method, ModuleDefinition module) {
			var type = method.DeclaringType;
			var import = type.Import(module);
			var typeDef = import.Resolve();
			var match = typeDef.Methods
				.SingleOrDefault(md => CecilTypeComparer.IsEqual(method, md));
			if (match == null)
				throw new NotImplementedException();

			var methodImport = match.IndirectMethodReference(module, import);
			var imported = methodImport.ImportInternal(module, import);
			return imported;
		}

		public static MethodReference Import(this MethodReference method, ModuleDefinition module) {
			var type = method.DeclaringType;
			var import = type.Import(module);
			var typeDef = import.Resolve();
			var match = typeDef.Methods
				.SingleOrDefault(md => CecilTypeComparer.IsEqual(method, md));
			if (match == null)
				throw new NotImplementedException();

			var methodImport = match.IndirectMethodReference(module, import);
			var imported = methodImport.ImportInternal(module, import);
			return imported;
		}

		public static MethodReference Import(this ConstructorInfo method, ModuleDefinition module) {
			var type = method.DeclaringType;
			var import = type.Import(module);
			var typeDef = import.Resolve();
			var match = typeDef.Methods
				.SingleOrDefault(md => md.IsConstructor && CecilTypeComparer.IsEqual(method, md));
			if (match == null)
				throw new NotImplementedException();

			var methodImport = match.IndirectMethodReference(module, import);
			var imported = methodImport.ImportInternal(module, import);
			return imported;
		}

		private static MethodReference ImportInternal(this MethodReference method, ModuleDefinition module, TypeReference import) {
			if ( method is MethodDefinition )
				throw new NotSupportedException();
			method.ReturnType = method.ReturnType.Import(module);
			foreach (var param in method.Parameters)
				param.ParameterType = param.ParameterType.Import(module);
			for (var i = 0 ; i < method.GenericParameters.Count ; ++i) {
				var param = method.GenericParameters[i];
				var paramImport = param.Import(module) as GenericParameter;
				method.GenericParameters[i] = paramImport
											?? throw new NotImplementedException();
			}

			method.DeclaringType = import;
			var imported = module.ImportReference(method);
			return imported;
		}

		public static MethodReference IndirectMethodReference(this MethodDefinition methodDef, ModuleDefinition module, TypeReference import) {
			var mr = new MethodReference(methodDef.Name, methodDef.ReturnType, import.Import(module)) {
				CallingConvention = methodDef.CallingConvention,
				ExplicitThis = methodDef.ExplicitThis,
				HasThis = methodDef.HasThis,

				//MetadataToken = import.MetadataToken,
				//MethodReturnType = methodDef.MethodReturnType,
			};
			foreach (var p in methodDef.Parameters) {
				var pr = p.Import(methodDef, module);
				mr.Parameters.Add(pr.Resolve());
			}
			foreach (var p in methodDef.GenericParameters)
				mr.GenericParameters.Add(p);

			return mr;
		}

		public static ParameterReference Import(this ParameterDefinition p, MethodReference mr, ModuleDefinition module) {
			return new ParameterReference(p.Name, p.Index, p.ParameterType.Import(module), mr) {
				MetadataToken = p.MetadataToken
			};
		}

		public static TypeReference Import(this TypeReference type, ModuleDefinition module) {
			if (type.IsGenericParameter)
				return type;

			var interiorType = type.GetInteriorType(out var txfs);
			interiorType = ImportInternal(module, interiorType);
			foreach (var txf in txfs) {
				txf(ref interiorType);
				interiorType = module.ImportReference(interiorType);
			}

			return interiorType;
		}

		public static TypeReference Import(this Type type, ModuleDefinition module) {
			var interiorType = type.GetInteriorType(out var transforms);

			var tr = ImportInternal(module, interiorType);

			var imported = tr.ApplyTransforms(transforms);
			return imported;
		}

		private static TypeReference ImportInternal(ModuleDefinition module, TypeReference type) {
			TypeReference tr;
			if (module.TryGetTypeReference(type.FullName, out tr))
				return tr;

			tr = module.FindInTypeSystem(type.FullName);
			if (tr != null) {
				//tr = FindNonPrivateForwarder(module, tr.FullName);
				return tr; //.Import(module);
			}

			if (type.IsPrimitive)
				throw new NotImplementedException();

			tr = module.GetType(type.FullName);
			if (tr != null)
				return tr;

			foreach (var asmRef in module.AssemblyReferences) {
				var asm = module.AssemblyResolver.Resolve(asmRef);
				if (asm == null)
					continue;

				tr = asm.MainModule.GetType(type.FullName);
				if (tr != null)
					return module.ImportReference(tr);
			}

			tr = module.ImportReference(type);

			if (tr.Scope.Name.StartsWith("System.Private")) {
				// try to import a forwarded type reference instead
				tr = FindNonPrivateForwarder(module, tr.FullName)
					.Import(module);
			}
			return tr;
		}


		private static TypeReference ImportInternal(ModuleDefinition module, Type type) {
			TypeReference tr;
			if (module.TryGetTypeReference(type.FullName, out tr))
				return tr;

			tr = module.FindInTypeSystem(type);
			if (tr != null) {
				//tr = FindNonPrivateForwarder(module, tr.FullName);
				return tr; //.Import(module);
			}

			if (type.GetTypeInfo().IsPrimitive)
				throw new NotImplementedException();

			tr = module.GetType(type.FullName);
			if (tr != null)
				return tr;

			foreach (var asmRef in module.AssemblyReferences) {
				var asm = module.AssemblyResolver.Resolve(asmRef);
				if (asm == null)
					continue;

				tr = asm.MainModule.GetType(type.FullName);
				if (tr != null)
					return tr.Import(module);
			}

			var typeInfo = type.GetTypeInfo();

			if (typeInfo.Assembly.FullName.StartsWith("System.Private")) {
				// try to import a forwarded type reference instead
				tr = FindNonPrivateForwarder(module, typeInfo.FullName)
					.Import(module);
				return tr;
			}

			tr = module.ImportReference(typeInfo);
			return tr;
		}

		private static TypeReference FindNonPrivateForwarder(ModuleDefinition module, string privateTypeRef) {
			var typeNestings = privateTypeRef.Split('+', '/');
			var baseTypeRef = typeNestings[0];
			TypeReference tr = null;
			TypeDefinition td = null;
			foreach (var asmName in AssemblyResolver.KnownAssemblies.Keys
				.Where(k => !k.StartsWith("System.Private") && k != "mscorlib")) {
				var asmDef = module.AssemblyResolver
					.Resolve(new AssemblyNameReference(asmName, null));

				foreach (var asmMod in asmDef.Modules) {
					var match = asmMod.ExportedTypes
						.FirstOrDefault(t => t.FullName == baseTypeRef);

					if (match == null)
						continue;

					td = match.Resolve();
					if (!match.IsForwarder) {
						tr = td;
						break;
					}

					var scope = asmMod.Assembly.Name.Name == module.TypeSystem.CoreLibrary.Name
						? module.TypeSystem.CoreLibrary
						: asmMod;

					tr = CreateTypeReference(td, asmMod, scope);
					break;
				}

				if (tr != null)
					break;
			}

			if (tr == null)
				return null;

			if (typeNestings.Length == 1)
				return tr;

			if (td == null || td.FullName != tr.FullName)
				td = tr.Resolve();
			td = typeNestings.Skip(1).Aggregate(td,
				(current, typeNesting) => current?.NestedTypes
					.FirstOrDefault(nt => nt.Name == typeNesting));
			if (td == null)
				return null;

			tr = CreateTypeReference(td, tr.Module);
			return tr;
		}


		public static TypeReference CreateTypeReference(TypeReference type, ModuleDefinition module, IMetadataScope scope = null) {
			if (!type.IsNested) {
				return new TypeReference(
					type.Namespace,
					type.Name,
					module,
					scope ?? module,
					type.IsValueType);
			}

			return new TypeReference(
				"",
				type.Name,
				module,
				scope ?? module,
				type.IsValueType) {
				DeclaringType = CreateTypeReference(type.DeclaringType, module)
			};
		}

		public static TypeReference FindInTypeSystem(this ModuleDefinition module, Type interiorType)
			=> interiorType == typeof(void)
				? module.TypeSystem.Void
				: interiorType == typeof(bool)
					? module.TypeSystem.Boolean
					: interiorType == typeof(byte)
						? module.TypeSystem.Byte
						: interiorType == typeof(sbyte)
							? module.TypeSystem.SByte
							: interiorType == typeof(char)
								? module.TypeSystem.Char
								: interiorType == typeof(short)
									? module.TypeSystem.Int16
									: interiorType == typeof(ushort)
										? module.TypeSystem.UInt16
										: interiorType == typeof(int)
											? module.TypeSystem.Int32
											: interiorType == typeof(uint)
												? module.TypeSystem.UInt32
												: interiorType == typeof(long)
													? module.TypeSystem.Int64
													: interiorType == typeof(ulong)
														? module.TypeSystem.UInt64
														: interiorType == typeof(float)
															? module.TypeSystem.Single
															: interiorType == typeof(double)
																? module.TypeSystem.Double
																: interiorType == typeof(IntPtr)
																	? module.TypeSystem.IntPtr
																	: interiorType == typeof(UIntPtr)
																		? module.TypeSystem.UIntPtr
																		: interiorType == typeof(string)
																			? module.TypeSystem.String
																			: interiorType == typeof(object)
																				? module.TypeSystem.Object
																				: interiorType == typeof(ValueType)
																					? module.TypeSystem.ValueType
																					: interiorType == typeof(Enum)
																						? module.TypeSystem.Enum
																						: null;


		public static TypeReference FindInTypeSystem(this ModuleDefinition module, string typeName)
			=> typeName == typeof(void).FullName
				? module.TypeSystem.Void
				: typeName == typeof(bool).FullName
					? module.TypeSystem.Boolean
					: typeName == typeof(byte).FullName
						? module.TypeSystem.Byte
						: typeName == typeof(sbyte).FullName
							? module.TypeSystem.SByte
							: typeName == typeof(char).FullName
								? module.TypeSystem.Char
								: typeName == typeof(short).FullName
									? module.TypeSystem.Int16
									: typeName == typeof(ushort).FullName
										? module.TypeSystem.UInt16
										: typeName == typeof(int).FullName
											? module.TypeSystem.Int32
											: typeName == typeof(uint).FullName
												? module.TypeSystem.UInt32
												: typeName == typeof(long).FullName
													? module.TypeSystem.Int64
													: typeName == typeof(ulong).FullName
														? module.TypeSystem.UInt64
														: typeName == typeof(float).FullName
															? module.TypeSystem.Single
															: typeName == typeof(double).FullName
																? module.TypeSystem.Double
																: typeName == typeof(IntPtr).FullName
																	? module.TypeSystem.IntPtr
																	: typeName == typeof(UIntPtr).FullName
																		? module.TypeSystem.UIntPtr
																		: typeName == typeof(string).FullName
																			? module.TypeSystem.String
																			: typeName == typeof(object).FullName
																				? module.TypeSystem.Object
																				: typeName == typeof(ValueType).FullName
																					? module.TypeSystem.ValueType
																					: typeName == typeof(Enum).FullName
																						? module.TypeSystem.Enum
																						: null;

		private static int GetSizeOfPrimitive(TypeReference typeRef, int pointerSize = -1) {
			switch (typeRef.FullName) {
				case "System.Void": return 0;
				case "System.Boolean":
				case "System.SByte":
				case "System.Byte": return 1;
				case "System.Char":
				case "System.Int16":
				case "System.UInt16": return 2;
				case "System.Single":
				case "System.Int32":
				case "System.UInt32": return 4;
				case "System.Double":
				case "System.Int64":
				case "System.UInt64": return 8;
				case "System.IntPtr":
				case "System.UIntPtr":
					return pointerSize == -1
						? IntPtr.Size
						: pointerSize;
				default:
					throw new NotImplementedException();
			}
		}

		public static Type GetRuntimeType(this TypeReference type) {
			var t = Type.GetType(type.FullName, false);
			if (t == null)
				t = Type.GetType(type.FullName.Replace('/', '+'), false);
			if (t == null)
				throw new NotImplementedException(type.FullName);

			return t;
		}

		public static bool IsDirect(this TypeReference type)
			=> !type.IsIndirect();

		public static bool IsIndirect(this TypeReference type)
			=> type.IsArray || type.IsPointer || type.IsByReference;

		public static bool IsDirectOrArray(this TypeReference type)
			=> !(type.IsPointer || type.IsByReference);

		public static bool IsSpecific(this MetadataType mdt) {
			switch (mdt) {
				case MetadataType.Boolean:
				case MetadataType.SByte:
				case MetadataType.Int16:
				case MetadataType.Int32:
				case MetadataType.Int64:
				case MetadataType.IntPtr:
				case MetadataType.Byte:
				case MetadataType.UInt16:
				case MetadataType.UInt32:
				case MetadataType.UInt64:
				case MetadataType.UIntPtr:
				case MetadataType.Char:
				case MetadataType.Single:
				case MetadataType.Double:
				case MetadataType.Void:
				case MetadataType.String:
				case MetadataType.Pointer:
				case MetadataType.FunctionPointer:
					return true;
			}

			return false;
		}

		// ReSharper disable once CyclomaticComplexity
		public static bool IsPrimitive(this TypeReference typeRef) {
			if (typeRef.IsPrimitive)
				return true;

			switch (typeRef.MetadataType) {
				case MetadataType.ValueType: {
					if (typeRef is TypeDefinition)
						return false;

					var typeDef = typeRef.Resolve();
					return typeDef.IsPrimitive
							|| typeDef.IsPrimitive();
				}
				case MetadataType.Boolean:
				case MetadataType.SByte:
				case MetadataType.Int16:
				case MetadataType.Int32:
				case MetadataType.Int64:
				case MetadataType.IntPtr:
				case MetadataType.Byte:
				case MetadataType.UInt16:
				case MetadataType.UInt32:
				case MetadataType.UInt64:
				case MetadataType.UIntPtr:
				case MetadataType.Char:
				case MetadataType.Single:
				case MetadataType.Double:
					return true;
				case MetadataType.Void:
				case MetadataType.Array:
				case MetadataType.ByReference:
				case MetadataType.FunctionPointer:
				case MetadataType.Class:
				case MetadataType.Object:
				case MetadataType.GenericInstance:
				case MetadataType.String:
				case MetadataType.Pointer:
					return false;
			}

			throw new NotImplementedException();
		}

		public static int SizeOf(this TypeReference type, int pointerSize = -1)
			=> type.IsPointer || type.IsByReference
				? IntPtr.Size
				: type.IsPrimitive()
					? GetSizeOfPrimitive(type, pointerSize)
					: type.Resolve().ClassSize;

		public static void DefineMethodOverride(this TypeDefinition typeDef, MethodDefinition overrider, MethodReference overridden)
			=> overrider.Overrides.Add(overridden);

		public static PropertyReference GetProperty(this TypeDefinition typeDef, string propName)
			=> typeDef.Properties.First(prop => prop.Name == propName);


		public static PropertyReference GetProperty(this TypeDefinition typeDef, string propName, BindingFlags bf) {
			if ((AnyAccessInstanceOrStaticBindingFlags & bf) == AnyAccessInstanceOrStaticBindingFlags)
				return typeDef.Properties.Single(md => md.Name == propName);

			return typeDef.Properties.Single

				// ReSharper disable once CyclomaticComplexity
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
			var innerOtherRef = GetInteriorType(otherRef, out var otherTxfs);
			var otherDef = innerOtherRef.Resolve();

			foreach (var typeRef in typeRefs) {
				if (typeRef.MetadataType.IsSpecific()
					&& otherRef.MetadataType.IsSpecific()
					&& typeRef.MetadataType != otherRef.MetadataType)
					return false;

				var innerTypeRef = GetInteriorType(typeRef, out var typeTxfs);
				if (!typeTxfs.SequenceEqual(otherTxfs))
					continue;

				var typeDef = innerTypeRef.Resolve();

				if (otherDef == null && typeDef == null
					&& typeRef.FullName == otherRef.FullName)
					return true;

				if (otherDef == null || typeDef == null)
					continue;

				if (otherDef == typeDef)
					return true;

				if (typeDef.Scope.MetadataScopeType != otherDef.Scope.MetadataScopeType)
					throw new NotImplementedException();

				if (typeDef.MetadataType != otherDef.MetadataType)
					continue;

				if (typeDef.MetadataToken.ToUInt32() != otherDef.MetadataToken.ToUInt32())
					continue;

				if (typeDef.Scope.MetadataToken.ToUInt32() != otherDef.Scope.MetadataToken.ToUInt32())
					continue;

				return true;
			}

			return false;
		}

		public static bool Is(this TypeReference typeRef, TypeReference otherRef) {
			StackGuard.DebugLimitEntry(64);

			if (typeRef.MetadataType.IsSpecific()
				&& otherRef.MetadataType.IsSpecific()
				&& typeRef.MetadataType != otherRef.MetadataType)
				return false;

			var innerTypeRef = GetInteriorType(typeRef, out var typeTxfs);
			var innerOtherRef = GetInteriorType(otherRef, out var otherTxfs);

			if (!typeTxfs.SequenceEqual(otherTxfs))
				return false;

			var typeDef = innerTypeRef.Resolve();
			var otherDef = innerOtherRef.Resolve();

			if (typeDef == null && otherDef == null
				&& typeRef.FullName == otherRef.FullName)
				return true;

			if (typeDef == null || otherDef == null)
				return false;

			if (otherDef == typeDef)
				return true;

			if (typeDef.Scope.MetadataScopeType != otherDef.Scope.MetadataScopeType)
				throw new NotImplementedException();

			if (typeDef.MetadataType != otherDef.MetadataType)
				return false;

			if (typeDef.MetadataToken.ToUInt32() != otherDef.MetadataToken.ToUInt32())
				return false;

			if (typeDef.Scope.MetadataToken.ToUInt32() != otherDef.Scope.MetadataToken.ToUInt32())
				return false;

			return true;
		}

		// ReSharper disable once CyclomaticComplexity
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

				if (typeRef.IsByReference && typeRef.IsByReference) {
					do {
						typeRef = typeRef.DescendElementType();
						otherRef = otherRef.DescendElementType();
					} while (typeRef.IsByReference && otherRef.IsByReference);

					continue;
				}

				if (typeRef.IsPointer != otherRef.IsPointer)
					return false;

				if (typeRef.IsPointer && otherRef.IsPrimitive) {
					do {
						typeRef = typeRef.DescendElementType();
						otherRef = otherRef.DescendElementType();
					} while (typeRef.IsPointer && otherRef.IsPointer);

					continue;
				}

				if (typeRef.IsArray != otherRef.IsArray)
					return false;

				if (typeRef.IsArray && otherRef.IsArray) {
					do {
						typeRef = typeRef.DescendElementType();
						otherRef = otherRef.DescendElementType();
					} while (typeRef.IsArray && otherRef.IsArray);

					continue;
				}

				if (typeRef.Is(otherRef))
					return true;

				var typeDef = typeRef.Resolve();
				if (typeDef != null) {
					var otherDef = otherRef.Resolve();

					if (typeDef.IsInterface
						&& otherDef.Interfaces.Contains(typeDef))
						return true;

					otherRef = otherDef.BaseType;
					continue;
				}

				throw new NotImplementedException();
			}
		}

		public delegate void TypeReferenceTransform(ref TypeReference t);

		public static void MakeArrayType(ref TypeReference t) => t = t.MakeArrayType();

		public static void MakeByRefType(ref TypeReference t) => t = t.MakeByReferenceType();

		public static void MakePointerType(ref TypeReference t) => t = t.MakePointerType();

		public static TypeReference GetTypePointedTo(this ModuleDefinition module, Type exterior, out LinkedList<TypeReferenceTransform> transform, bool directVoids = false)
			=> GetTypePointedTo(exterior.Import(module), out transform, directVoids);

		public static TypeReference GetTypePointedTo(this TypeReference exterior, out LinkedList<TypeReferenceTransform> transform, bool directVoids = false) {
			var type = exterior;
			transform = new LinkedList<TypeReferenceTransform>();
			while (type.IsPointer) {
				var module = exterior.Module;
				if (directVoids || !type.Is(module.TypeSystem.Void)) {
					transform.AddFirst(MakePointerType);
					type = type.DescendElementType();
				}
				else
					return type;
			}

			return type;
		}

		public static TypeReference DescendElementType(this TypeReference typeRef)
			=> typeRef is TypeSpecification typeSpec
				? typeSpec.ElementType
				: throw new NotImplementedException();

		public static TypeReference GetInteriorType(this TypeReference exterior, out LinkedList<TypeReferenceTransform> transforms, bool directVoids = false) {
			transforms = new LinkedList<TypeReferenceTransform>();
			return FindInteriorType(exterior, ref transforms, directVoids);
		}

		public static TypeReference GetInteriorType(this TypeReference exterior, bool directVoids = false) {
			var type = exterior;
			for (; ;) {
				if (type.IsPointer) {
					var module = exterior.Module;
					if (directVoids || !type.Is(module.TypeSystem.Void)) {
						type = (type as PointerType ?? throw new NotImplementedException()).ElementType;
					}
					else {
						break;
					}
				}

				// type.MetadataType == MetadataType.Array
				else if (type.IsArray) {
					type = (type as ArrayType ?? throw new NotImplementedException()).ElementType;
				}

				// type.MetadataType == MetadataType.ByReference
				// type.MetadataType == MetadataType.TypedByReference
				else if (type.IsByReference) {
					type = (type as ByReferenceType ?? throw new NotImplementedException()).ElementType;
				}
				else
					break;
			}

			return type;
		}

		public static TypeReference FindInteriorType(this TypeReference exterior, ref LinkedList<TypeReferenceTransform> transform, bool directVoids = false) {
			var type = exterior;
			for (; ;) {
				if (type.IsPointer) {
					var etype = ((PointerType) type).ElementType;
					if (etype.MetadataType == MetadataType.Void && !directVoids)
						break;

					transform.AddFirst(MakePointerType);
					type = etype;
				}

				// type.MetadataType == MetadataType.Array
				else if (type.IsArray) {
					transform.AddFirst(MakeArrayType);
					type = ((ArrayType) type).ElementType;
				}

				// type.MetadataType == MetadataType.ByReference
				// type.MetadataType == MetadataType.TypedByReference
				else if (type.IsByReference) {
					transform.AddFirst(MakeByRefType);
					type = ((ByReferenceType) type).ElementType;
				}
				else
					break;
			}
			Debug.Assert(type.IsDirectOrArray() || type.Name=="Void*");
			return type;
		}

	}
}