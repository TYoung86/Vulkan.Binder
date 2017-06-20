using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Artilect.Vulkan.Binder.Extensions;
using static Artilect.Vulkan.Binder.Extensions.CecilExtensions;
//using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Artilect.Vulkan.Binder {
	[DebuggerDisplay("~ {" + nameof(FullName) + "}")]
	public class IncompleteTypeReference : TypeReference,
		IEquatable<IncompleteTypeReference>,
		IComparable<IncompleteTypeReference>,
		IEquatable<TypeReference>,
		IComparable<TypeReference> {
		public static void Require(ref TypeReference type, IDictionary<string,string> typeRedirs, bool tryInterface, params string[] suffixes) {
			var isPtr = type.IsPointer;
			if (type == null)
				throw new InvalidProgramException("Null was passed to require.");

			var interiorType = type.GetInteriorType(out var transforms);

			if (!(interiorType is IncompleteTypeReference incompleteType)) return;

			var resolvedType = interiorType;
			while (resolvedType is IncompleteTypeReference) {
				resolvedType = incompleteType.ForceResolveType(typeRedirs, tryInterface, suffixes);
			}

			if (resolvedType == null)
				throw new TypeLoadException($"Can't resolve type {type.FullName}.");

			foreach (var transform in transforms)
				transform(ref resolvedType);

			// todo: remove this sanity check
			if (resolvedType.IsPointer != isPtr)
				throw new Exception("Doh");

			type = resolvedType;
		}

		public static TypeReference Require(TypeReference type, IDictionary<string,string> typeRedirs, bool tryInterface, params string[] suffixes) {
			Require(ref type, typeRedirs, tryInterface, suffixes);
			return type;
		}

		public static void Require(IDictionary<string,string> typeRedirs, bool tryInterface, string[] suffixes, params TypeReference[] types) {
			for (var i = 0 ; i < types.Length ; ++i)
				types[i] = Require(types[i], typeRedirs, tryInterface, suffixes);
		}

		public bool CanResolveNow { get; set; }
		public ModuleDefinition LookupModule { get; set; }
		public string LookupNamespace { get; set; }
		private string _lookupName;

		public string LookupName {
			get => _lookupName;
			set => _lookupName = !string.IsNullOrEmpty(value)
				? value
				: throw new ArgumentNullException(nameof(LookupName));
		}

		public enum IndirectionType {
			None,
			Pointer,
			Reference,
			Array
		}

		private IndirectionType IndirectType { get; set; }

		private TypeReference _type;

		private TypeReference Type {
			set => _type = value;
			get {
				if (IndirectType == IndirectionType.None && _type != null)
					return _type;
				return ResolveType();
			}
		}

		internal static readonly ConcurrentDictionary<TypeReference, TypeReference> Interned
			= new ConcurrentDictionary<TypeReference, TypeReference>();

		private static TypeReference Intern(TypeReference type) {
			if (type == null) return null;
			if (type is IncompleteTypeReference incompleteType)
				return Intern(incompleteType);
			Interned[type] = type;
			return type;
		}

		private static TypeReference Intern(IncompleteTypeReference incompleteType) {
			if (incompleteType == null) return null;
			return Interned.GetOrAdd(incompleteType, k => k);
		}

		public static TypeReference Get(ModuleDefinition mod, string @namespace, string name, bool canResolveNow = true) {
			var incompleteType = new IncompleteTypeReference(mod, @namespace, name, canResolveNow);
			return Intern(incompleteType);
		}

		public static TypeReference Get(IncompleteTypeReference type, IndirectionType indirType) {
			var incompleteType = new IncompleteTypeReference(type, indirType);
			return Intern(incompleteType);
		}

		protected IncompleteTypeReference(ModuleDefinition mod, string @namespace, string name, bool canResolveNow = true)
			: base(@namespace, name) {
			CanResolveNow = canResolveNow;
			LookupModule = mod;
			LookupNamespace = @namespace;
			LookupName = name;

			if (LookupNamespace != null) return;
			var lastIndexOfDot = LookupName.LastIndexOf(".", StringComparison.Ordinal);
			if (lastIndexOfDot == -1) return;
			LookupNamespace = LookupName.Substring(0, lastIndexOfDot);
			LookupName = LookupName.Substring(lastIndexOfDot + 1);
		}

		protected IncompleteTypeReference(IncompleteTypeReference type, IndirectionType indirType)
			: base(type.LookupNamespace, type.Name){
			LookupModule = type.LookupModule;
			LookupNamespace = type.LookupNamespace;
			LookupName = type.Name;
			CanResolveNow = false;
			IndirectType = indirType;
			Type = type;
		}

		public TypeReference ForceResolveType(IDictionary<string,string> typeRedirs, bool tryInterface, params string[] suffixes) {
			if (typeRedirs.TryGetValue(LookupName, out var renamed)) {
				LookupName = renamed;
			}

			return ForceResolveType(tryInterface, suffixes);
		}

		public TypeReference ForceResolveType(bool tryInterface, params string[] suffixes) {
			return Intern(ForceResolveTypeInternal(tryInterface, suffixes));
		}

		private TypeReference ForceResolveTypeInternal(bool tryInterface, params string[] suffixes) {
			if (_type != null && !(_type is IncompleteTypeReference)) {
				switch (IndirectType) {
					case IndirectionType.Pointer:
						return _type.MakePointerType();
					case IndirectionType.Array:
						return _type.MakeArrayType();
					case IndirectionType.Reference:
						return _type.MakeByReferenceType();
					default:
						return _type;
				}
			}

			if (FullName == null)
				return null;

			var names = new string[1 + (tryInterface ? 1 : 0) + suffixes.Length];
			var i = 0;
			names[i++] = FullName;
			var noNamespace = Namespace == null;
			if (tryInterface)
				names[i++] = noNamespace
					? $"I{Name}{Decoration}"
					: $"{Namespace}.I{Name}{Decoration}";
			foreach (var suffix in suffixes)
				names[i++] = noNamespace
					? $"{Name}{suffix}{Decoration}"
					: $"{Namespace}.{Name}{suffix}{Decoration}";

			if (ForceResolveTypeInternalByModule(names))
				return _type;

			return null;
		}

		private bool ForceResolveTypeInternalByModule(string[] names) {
			if (LookupModule != null) {
				foreach (var name in names) {
					var possibleType = LookupModule.GetType(name);
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
					_type = _type.MakeByReferenceType();
					break;
			}
		}

		public TypeReference ResolveType() {
			if (!CanResolveNow)
				return null;

			var resolved = ForceResolveType(false);

			return resolved;
		}

		public override bool IsArray {
			get {
				if (IndirectType == IndirectionType.None)
					return _type?.IsArray ?? false;
				return IndirectType == IndirectionType.Array;
			}
		}

		public override bool IsByReference {
			get {
				if (IndirectType == IndirectionType.None)
					return _type?.IsByReference ?? false;
				return IndirectType == IndirectionType.Reference;
			}
		}

		public override bool IsPointer {
			get {
				if (IndirectType == IndirectionType.None)
					return _type?.IsPointer ?? false;
				return IndirectType == IndirectionType.Pointer;
			}
		}

		public override string Name => _type?.Name ?? LookupName;

		public override ModuleDefinition Module => Type?.Module ?? LookupModule ?? throw new InvalidOperationException();

		private string AdditionalDecoration => (_type as IncompleteTypeReference)?.Decoration ?? "";

		public string Decoration => AdditionalDecoration + (IsArray ? "[]" : IsPointer ? "*" : IsByReference ? "&" : "");

		public override string FullName => _type?.FullName
											?? ((LookupNamespace == null ? LookupName : LookupNamespace + "." + LookupName) +
												Decoration);

		public override string Namespace => _type?.Namespace ?? LookupNamespace;

		public TypeReference MakePointerType() {
			if (IndirectType == IndirectionType.None) {
				var pointerType = _type?.MakePointerType();
				if (pointerType != null)
					return pointerType;
			}
			return new IncompleteTypeReference(this, IndirectionType.Pointer);
		}

		public TypeReference MakeArrayType() {
			if (IndirectType == IndirectionType.None) {
				var pointerType = _type?.MakeArrayType();
				if (pointerType != null)
					return pointerType;
			}
			return new IncompleteTypeReference(this, IndirectionType.Array);
		}

		public TypeReference MakeByReferenceType() {
			if (IndirectType == IndirectionType.None) {
				var pointerType = _type?.MakeByReferenceType();
				if (pointerType != null)
					return pointerType;
			}
			return new IncompleteTypeReference(this, IndirectionType.Reference);
		}

		int IComparable<IncompleteTypeReference>.CompareTo(IncompleteTypeReference other)
			=> CompareTo(other);

		public int CompareTo(TypeReference other)
			=> StringComparer.Ordinal.Compare(
				FullName ?? FullName ?? Name,
				other.FullName ?? other.FullName ?? Name);

		public override int GetHashCode() {
			return Name?.GetHashCode() ?? 0;
		}

		public bool Equals(TypeReference other) {
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			return FullName == other.FullName;
		}

		public bool Equals(IncompleteTypeReference other) {
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			if (IndirectType == IndirectionType.None && _type != null && other.IndirectType == IndirectionType.None && _type == other._type) return true;
			return FullName == other.FullName;
		}
	}
}