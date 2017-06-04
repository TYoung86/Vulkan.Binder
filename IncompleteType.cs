using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Artilect.Vulkan.Binder {
	[DebuggerDisplay("~ {" + nameof(AssemblyQualifiedName) + "}")]
	public class IncompleteType : Type,
		IEquatable<IncompleteType>,
		IComparable<IncompleteType>,
		IEquatable<Type>,
		IComparable<Type> {
		public static void Require(ref Type type, bool tryInterface, params string[] suffixes) {
			var isPtr = type.IsPointer;
			if (type == null)
				throw new InvalidProgramException("Null was passed to require.");

			if (!(type is IncompleteType incompleteType)) return;
			var resolvedType = type;
			while (resolvedType != null && resolvedType is IncompleteType) {
				resolvedType = incompleteType.ForceResolveType(tryInterface, suffixes);
			}

			if (resolvedType == null)
				throw new TypeLoadException($"Can't resolve type {type.AssemblyQualifiedName}.");
			if ( resolvedType.IsPointer != isPtr)
				throw new Exception("Doh");
			type = resolvedType;
		}

		public static Type Require(Type type, bool tryInterface, params string[] suffixes) {
			Require(ref type, tryInterface, suffixes);
			return type;
		}

		public static void Require(bool tryInterface, string[] suffixes, params Type[] types) {
			for (var i = 0 ; i < types.Length ; ++i)
				types[i] = Require(types[i], tryInterface, suffixes);
		}

		public bool CanResolveNow { get; set; }
		public Assembly LookupAssembly { get; set; }
		public Module LookupModule { get; set; }
		public string LookupNamespace { get; set; }
		private string _lookupName;

		public string LookupName {
			get => _lookupName;
			set => _lookupName = !string.IsNullOrEmpty(value) ? value
				: throw new ArgumentNullException(nameof(LookupName));
		}

		public enum IndirectionType {
			None,
			Pointer,
			Reference,
			Array
		}

		private IndirectionType IndirectType { get; set; }

		private Type _type;

		private Type Type {
			set => _type = value;
			get {
				if (IndirectType == IndirectionType.None && _type != null)
					return _type;
				return ResolveType();
			}
		}

		internal static readonly ConcurrentDictionary<Type,Type> Interned
			= new ConcurrentDictionary<Type,Type>();

		private static Type Intern(Type type) {
			if (type == null) return null;
			if (type is IncompleteType incompleteType)
				return Intern(incompleteType);
			Interned[type] = type;
			return type;
		}

		private static Type Intern(IncompleteType incompleteType) {
			if (incompleteType == null) return null;
			return Interned.GetOrAdd(incompleteType, k => k);
		}

		public static Type Get(Assembly asm, Module mod, string @namespace, string name, bool canResolveNow = true) {
			var incompleteType = new IncompleteType(asm, mod, @namespace, name, canResolveNow);
			return Intern(incompleteType);
		}

		public static Type Get(IncompleteType type, IndirectionType indirType) {
			var incompleteType = new IncompleteType(type, indirType);
			return Intern(incompleteType);
		}

		protected IncompleteType(Assembly asm, Module mod, string @namespace, string name, bool canResolveNow = true) {

			CanResolveNow = canResolveNow;
			LookupAssembly = asm;
			LookupModule = mod;
			LookupNamespace = @namespace;
			LookupName = name;

			if (LookupNamespace != null) return;
			var lastIndexOfDot = LookupName.LastIndexOf(".", StringComparison.Ordinal);
			if (lastIndexOfDot == -1) return;
			LookupNamespace = LookupName.Substring(0, lastIndexOfDot);
			LookupName = LookupName.Substring(lastIndexOfDot+1);
		}

		protected IncompleteType(IncompleteType type, IndirectionType indirType) {
			LookupAssembly = type.LookupAssembly;
			LookupModule = type.LookupModule;
			LookupNamespace = type.LookupNamespace;
			LookupName = type.Name;
			CanResolveNow = false;
			IndirectType = indirType;
			Type = type;
		}

		public Type ForceResolveType(bool tryInterface, params string[] suffixes) {
			return Intern(ForceResolveTypeInternal(tryInterface, suffixes));
		}

		private Type ForceResolveTypeInternal(bool tryInterface, params string[] suffixes) {
			if (_type != null && !(_type is IncompleteType)) {
				switch (IndirectType) {
					case IndirectionType.Pointer:
						return _type.MakePointerType();
					case IndirectionType.Array:
						return _type.MakeArrayType();
					case IndirectionType.Reference:
						return _type.MakeByRefType();
					default:
						return _type;
				}
			}
			
			if (FullName == null)
				return null;

			var names = new string[1+(tryInterface?1:0)+suffixes.Length];
			var i = 0;
			names[i++] = FullName;
			var noNamespace = Namespace == null;
			if ( tryInterface )
				names[i++] = noNamespace
					?$"I{Name}{Decoration}"
					:$"{Namespace}.I{Name}{Decoration}";
			foreach ( var suffix in suffixes )
				names[i++] = noNamespace 
					?$"{Name}{suffix}{Decoration}"
					:$"{Namespace}.{Name}{suffix}{Decoration}";

			if (ForceResolveTypeInternalByModule(names))
				return _type;

			if (ForceResolveTypeInternalByAssembly(names)) return null;

			/*
			foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach (var name in names) {
					var possibleType = asm.GetType(name, false, false);
					if (possibleType == null)
						continue;
					_type = possibleType;
					IndirectType = IndirectionType.None;
					return _type;
				}
			}
			*/

			return null;
		}

		private bool ForceResolveTypeInternalByAssembly(string[] names) {
			if (LookupAssembly != null) {
				foreach (var name in names) {
					var possibleType = LookupAssembly.GetType(name, false, false);
					if (possibleType == null)
						continue;
					_type = possibleType;
					CorrectForIndirection();
					IndirectType = IndirectionType.None;
					return true;
				}
			}
			return false;
		}

		private bool ForceResolveTypeInternalByModule(string[] names) {
			if (LookupModule != null) {
				foreach (var name in names) {
					var possibleType = LookupModule.GetType(name, false, false);
					if (possibleType == null)
						continue;
					_type = possibleType;
					CorrectForIndirection();
					IndirectType = IndirectionType.None;
					return true;
				}
			}
			return false;
		}

		private void CorrectForIndirection() {
			switch (IndirectType) {
				case IndirectionType.Array:
					_type = _type.MakeArrayType();
					break;
				case IndirectionType.Pointer:
					_type = _type.MakePointerType();
					break;
				case IndirectionType.Reference:
					_type = _type.MakeByRefType();
					break;
			}
		}

		public Type ResolveType() {
			if (!CanResolveNow)
				return null;

			var resolved = ForceResolveType(false);

			return resolved;
		}


		public override object[] GetCustomAttributes(bool inherit) {
			return Type?.GetCustomAttributes(inherit) ?? new object[0];
		}

		public override bool IsDefined(Type attributeType, bool inherit) {
			return Type?.IsDefined(attributeType, inherit) ?? false;
		}

		public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) {
			return Type?.GetConstructors(bindingAttr) ?? new ConstructorInfo[0];
		}

		public override Type GetInterface(string name, bool ignoreCase) {
			return Type?.GetInterface(name, ignoreCase);
		}

		public override Type[] GetInterfaces() {
			return Type?.GetInterfaces() ?? new Type[0];
		}

		public override EventInfo GetEvent(string name, BindingFlags bindingAttr) {
			return Type?.GetEvent(name, bindingAttr) ?? throw new MissingMemberException();
		}

		public override EventInfo[] GetEvents(BindingFlags bindingAttr) {
			return Type?.GetEvents(bindingAttr) ?? new EventInfo[0];
		}

		public override Type[] GetNestedTypes(BindingFlags bindingAttr) {
			return Type?.GetNestedTypes(bindingAttr) ?? new Type[0];
		}

		public override Type GetNestedType(string name, BindingFlags bindingAttr) {
			return Type?.GetNestedType(name, bindingAttr) ?? throw new MissingMemberException();
		}

		public override Type GetElementType() {
			if (_type != null && IndirectType != IndirectionType.None) {
				return _type;
			}
			return Type?.GetElementType();
		}

		protected override bool HasElementTypeImpl() {
			if (IndirectType == IndirectionType.Array || IndirectType == IndirectionType.Pointer)
				return true;
			if (IsArray || IsPointer)
				return true;
			try {
				return GetElementType() != null;
			}
			catch {
				return false;
			}
		}

		protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, System.Reflection.Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers) {
			return Type?.GetProperty(name, bindingAttr, binder, returnType, types, modifiers)
					?? throw new MissingMemberException();
		}

		public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) {
			return Type?.GetProperties(bindingAttr) ?? new PropertyInfo[0];
		}

		protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, System.Reflection.Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers) {
			return Type?.GetMethod(name, bindingAttr, binder, callConvention, types, modifiers)
					?? throw new MissingMethodException();
		}

		public override MethodInfo[] GetMethods(BindingFlags bindingAttr) {
			return Type?.GetMethods(bindingAttr) ?? new MethodInfo[0];
		}

		public override FieldInfo GetField(string name, BindingFlags bindingAttr) {
			return Type?.GetField(name, bindingAttr)
					?? throw new MissingFieldException();
		}

		public override FieldInfo[] GetFields(BindingFlags bindingAttr) {
			return Type?.GetFields(bindingAttr) ?? new FieldInfo[0];
		}

		public override MemberInfo[] GetMembers(BindingFlags bindingAttr) {
			return Type?.GetMembers(bindingAttr) ?? new MemberInfo[0];
		}

		protected override TypeAttributes GetAttributeFlagsImpl() {
			return Type?.GetTypeInfo()?.Attributes ?? default(TypeAttributes);
		}

		protected new bool IsArray => IsArrayImpl();

		protected override bool IsArrayImpl() {
			if (IndirectType == IndirectionType.None)
				return _type?.IsArray ?? false;
			return IndirectType == IndirectionType.Array;
		}

		protected new bool IsByRef => IsByRefImpl();

		protected override bool IsByRefImpl() {
			if (IndirectType == IndirectionType.None)
				return _type?.IsByRef ?? false;
			return IndirectType == IndirectionType.Reference;
		}

		protected new bool IsPointer => IsPointerImpl();

		protected override bool IsPointerImpl() {
			if (IndirectType == IndirectionType.None)
				return _type?.IsPointer ?? false;
			return IndirectType == IndirectionType.Pointer;
		}

		protected new bool IsPrimitive => IsPrimitiveImpl();

		protected override bool IsPrimitiveImpl() {
			return Type?.IsPrimitive ?? false;
		}

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		protected new bool IsCOMObject => IsCOMObjectImpl();

		protected override bool IsCOMObjectImpl() {
			return Type?.IsCOMObject ?? false;
		}

		public override object InvokeMember(string name, BindingFlags invokeAttr, System.Reflection.Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters) {
			return Type?.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters)
					?? throw new MissingMemberException();
		}

		public override Type UnderlyingSystemType => Type?.UnderlyingSystemType ?? this;

		protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, System.Reflection.Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers) {
			return Type?.GetConstructor(bindingAttr, binder, callConvention, types, modifiers)
					?? throw new MissingMethodException();
		}

		public override string Name => _type?.Name ?? LookupName;

		public override Guid GUID => Type?.GUID ?? Guid.Empty;

		public override Module Module => Type?.Module ?? LookupModule ?? throw new InvalidOperationException();

		public override Assembly Assembly => Type?.Assembly ?? LookupAssembly ?? throw new InvalidOperationException();

		private string AdditionalDecoration => (_type as IncompleteType)?.Decoration ?? "";

		public string Decoration => AdditionalDecoration + ( IsArray ? "[]" : IsPointer ? "*" : IsByRef ? "&" : "" );

		public override string FullName => _type?.FullName
											?? ((LookupNamespace == null ? LookupName : LookupNamespace + "." + LookupName) +
												Decoration);

		public override string Namespace => _type?.Namespace ?? LookupNamespace;

		public override string AssemblyQualifiedName => _type?.AssemblyQualifiedName
														?? Assembly.CreateQualifiedName(Assembly.GetName().ToString(), FullName);

		public override Type BaseType => Type?.BaseType ?? throw new InvalidOperationException();

		public override object[] GetCustomAttributes(Type attributeType, bool inherit) {
			return Type?.GetCustomAttributes(attributeType, inherit) ?? new object[0];
		}

		public override Type MakePointerType() {
			if (IndirectType == IndirectionType.None) {
				var pointerType = _type?.MakePointerType();
				if (pointerType != null)
					return pointerType;
			}
			return new IncompleteType(this, IndirectionType.Pointer);
		}

		public override Type MakeArrayType() {
			if (IndirectType == IndirectionType.None) {
				var pointerType = _type?.MakeArrayType();
				if (pointerType != null)
					return pointerType;
			}
			return new IncompleteType(this, IndirectionType.Array);
		}

		public override Type MakeByRefType() {
			if (IndirectType == IndirectionType.None) {
				var pointerType = _type?.MakeByRefType();
				if (pointerType != null)
					return pointerType;
			}
			return new IncompleteType(this, IndirectionType.Reference);
		}

		int IComparable<IncompleteType>.CompareTo(IncompleteType other)
			=> CompareTo(other);

		public int CompareTo(Type other)
			=> StringComparer.Ordinal.Compare(
				AssemblyQualifiedName ?? FullName ?? Name,
				other.AssemblyQualifiedName ?? other.FullName ?? Name);

		public override int GetHashCode() {
			return Name?.GetHashCode() ?? 0;
		}
		public override bool Equals(Type other) {
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			return AssemblyQualifiedName == other.AssemblyQualifiedName;
		}

		public bool Equals(IncompleteType other) {
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			if ( IndirectType == IndirectionType.None && _type != null && other.IndirectType == IndirectionType.None && _type == other._type) return true;
			return AssemblyQualifiedName == other.AssemblyQualifiedName;
		}
	}
}