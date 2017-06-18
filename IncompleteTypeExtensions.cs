namespace Artilect.Vulkan.Binder
{
    public static class IncompleteTypeReferenceExtensions
    {
	    public static void RequireCompleteTypeReferences(this ParameterInfo cpi, bool tryInterface, params string[] suffixes) {
		    IncompleteTypeReference.Require(ref cpi.Type, tryInterface, suffixes);
	    }

	    public static void RequireCompleteTypeReferences(this ParameterInfo cpi, params string[] suffixes) {
		    cpi.RequireCompleteTypeReferences(false, suffixes);
	    }

    }
}
