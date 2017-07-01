using System;
using System.Collections.Generic;
using Mono.Cecil;
using Vulkan.Binder.Extensions;

namespace Vulkan.Binder
{
    public static class IncompleteTypeReference
    {
	    public static TypeReference Get(ModuleDefinition module, string typeNs, string typeName) {
		    if (string.IsNullOrEmpty(typeNs))
			    typeNs = "";
		    else
			    typeNs = typeNs + '.';

		    var rtr = module.GetType(typeNs + typeName, true);
		    rtr.Scope = module;
		    return rtr;
	    }

	    public static void Complete(this ParameterInfo cpi, IDictionary<string,string> typeRedirs, bool tryInterface, params string[] suffixes) {
		    var tr = cpi.Type;

		    var result = tr.CompleteOrNull(tryInterface, typeRedirs, suffixes);

			cpi.Type = result
				?? throw new TypeLoadException($"Unable to complete {cpi.Type.FullName}");

	    }

	    public static TypeReference CompleteOrNull(this TypeReference tr, bool tryInterface = false, IDictionary<string, string> typeRedirs = null, params string[] suffixes) {
		    var outer = tr;
		    tr = tr.GetInteriorType(out var txfs, true);

		    if (tr is TypeDefinition)
			    return tr.ApplyTransforms(txfs);

		    var typeName = tr.Name;
		    var typeNs = tr.Namespace;
		    if (string.IsNullOrEmpty(typeNs))
			    typeNs = "";
		    else
			    typeNs = typeNs + '.';

		    ModuleDefinition module = null;
		    TypeReference result;
		    if (typeRedirs != null && typeRedirs.TryGetValue(typeName, out var redir)) {
			    module = tr.Module;
			    typeName = redir;
			    result = module.GetType(typeNs + typeName, true).Require(true);
		    }
		    else if (typeRedirs != null && typeRedirs.TryGetValue(typeNs + typeName, out var redirWithNs)) {
			    throw new NotImplementedException();
		    }
		    else {
			    result = tr.Require(true);
			    if (result == null) {
				    module = tr.Module;
				    result = module.GetType(typeNs + typeName, true).Require(true);
			    }
		    }

		    if (result != null)
				return result.ApplyTransforms(txfs);

		    if (module == null)
			    module = tr.Module;

		    if (tryInterface) {
			    result = module.GetType(typeNs + "I" + typeName, true).Require(true);
		    }

		    if (result != null)
			    return result.ApplyTransforms(txfs);;

		    if (suffixes == null)
				return null;

		    for (var i = 0 ; result == null && i < suffixes.Length ; ++i) {
			    var suffix = suffixes[i];
			    result = module.GetType(typeNs + typeName + suffix, true).Require(true);
		    }

		    return result.ApplyTransforms(txfs);;
	    }

	    public static void Complete(this ParameterInfo cpi, IDictionary<string,string> typeRedirs, params string[] suffixes) {
		    cpi.Complete(typeRedirs, false, suffixes);
	    }

    }
}
