namespace Artilect.Vulkan.Binder
{
    public static class IncompleteTypeExtensions
    {
	    public static void RequireCompleteTypes(this CustomParameterInfo cpi, bool tryInterface, params string[] suffixes) {
		    IncompleteType.Require(ref cpi.ParameterType, tryInterface, suffixes);
		    foreach (var attrInfo in cpi.AttributeInfos) {
			    for (var i = 0 ; i < attrInfo.Arguments.Length ; i++) {
				    var attrArg = attrInfo.Arguments[i];
				    if ( attrArg is IncompleteType incompleteType)
					    attrInfo.Arguments[i] = IncompleteType.Require(incompleteType, tryInterface, suffixes);
			    }
			    for (var i = 0 ; i < attrInfo.PropertyValues.Length ; i++) {
				    var propValue = attrInfo.PropertyValues[i];
				    if ( propValue is IncompleteType incompleteType)
					    attrInfo.PropertyValues[i] = IncompleteType.Require(incompleteType, tryInterface, suffixes);
			    }
			    for (var i = 0 ; i < attrInfo.FieldValues.Length ; i++) {
				    var fieldValue = attrInfo.FieldValues[i];
				    if ( fieldValue is IncompleteType incompleteType)
					    attrInfo.FieldValues[i] = IncompleteType.Require(incompleteType, tryInterface, suffixes);
			    }
		    }
	    }

	    public static void RequireCompleteTypes(this CustomParameterInfo cpi, params string[] suffixes) {
		    cpi.RequireCompleteTypes(false, suffixes);
	    }

    }
}
