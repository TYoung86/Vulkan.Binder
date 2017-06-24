using System.Collections.Generic;

namespace Vulkan.Binder
{
    public static class IncompleteTypeReferenceExtensions
    {
	    public static void RequireCompleteTypeReferences(this ParameterInfo cpi, IDictionary<string,string> typeRedirs, bool tryInterface, params string[] suffixes) {
		    IncompleteTypeReference.Require(ref cpi.Type, typeRedirs, tryInterface, suffixes);
	    }

	    public static void RequireCompleteTypeReferences(this ParameterInfo cpi, IDictionary<string,string> typeRedirs, params string[] suffixes) {
		    cpi.RequireCompleteTypeReferences(typeRedirs, false, suffixes);
	    }

    }
}
