using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Vulkan.Binder {
    public class VkXmlConsumer {

	    public readonly IDictionary<string, string> Constants = new ConcurrentDictionary<string, string>();
	    public readonly IDictionary<string, XElement> UncategorizedTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> DefineTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> StructTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> FunctionPointerTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> HandleTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> BitmaskTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> EnumTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> UnionTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> IncludeTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> BaseTypeTypes = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> Commands = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> InstanceExtensions = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> DeviceExtensions = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, XElement> Features = new ConcurrentDictionary<string, XElement>();
	    public readonly IDictionary<string, Dictionary<string, string>> Enums = new ConcurrentDictionary<string, Dictionary<string, string>>();
	    public readonly IDictionary<string, Dictionary<string, int>> Bitmasks = new ConcurrentDictionary<string, Dictionary<string, int>>();


        public VkXmlConsumer(XDocument document) {
            var registryElem = document.Root
                               ?? throw new ArgumentException("Document has no root element.", nameof(document));

            if (registryElem.Name != "registry")
                throw new ArgumentException("Document 'registry' must be the root element.", nameof(document));

            var typesElem = registryElem.Element("types")
                            ?? throw new ArgumentException("Document contains no 'types' element.", nameof(document));

            var typeElems = typesElem.Elements("type");

	        Parallel.ForEach(typeElems, typeElem => {
		        var category = typeElem.Attribute("category")?.Value;
		        var name = typeElem.Element("name")?.Value
							?? typeElem.Attribute("name")?.Value;
		        if (category == null) {
			        UncategorizedTypes.Add(name, typeElem);
			        return;
		        }
		        switch (category) {
			        default:
				        throw new NotImplementedException(category);
			        case "define":
				        DefineTypes.Add(name, typeElem);
				        return;
			        case "include":
				        IncludeTypes.Add(name, typeElem);
				        return;
			        case "struct":
				        StructTypes.Add(name, typeElem);
				        return;
			        case "funcpointer":
				        FunctionPointerTypes.Add(name, typeElem);
				        return;
			        case "handle":
				        HandleTypes.Add(name, typeElem);
				        return;
			        case "bitmask":
				        BitmaskTypes.Add(name, typeElem);
				        return;
			        case "enum":
				        EnumTypes.Add(name, typeElem);
				        return;
			        case "union":
				        UnionTypes.Add(name, typeElem);
				        return;
			        case "basetype":
				        BaseTypeTypes.Add(name, typeElem);
				        return;
		        }
	        });


            var commandsElem = registryElem.Element("commands")
                               ?? throw new ArgumentException("Document contains no 'commands' element.", nameof(document));

            var commandElems = commandsElem.Elements("command");


	        Parallel.ForEach(commandElems, commandElem => {
		        var name = commandElem.Element("proto")?.Element("name")?.Value
							?? commandElem.Attribute("name")?.Value;
		        Commands.Add(name, commandElem);
	        });

	        var featureElem = registryElem.Element("feature")
								?? throw new ArgumentException("Document contains no 'feature' element.", nameof(document));

	        var featureReqireElems = featureElem.Elements("require");


	        Parallel.ForEach(featureReqireElems, requireElem => {
		        var name = requireElem.Element("name")?.Value
					?? requireElem.Attribute("comment")?.Value;
				if ( name != null )
					Features.Add(name, requireElem);
	        });

            var extensionsElem = registryElem.Element("extensions")
                                 ?? throw new ArgumentException("Document contains no 'extensions' element.", nameof(document));

            var extensionElems = extensionsElem.Elements("extension");


	        Parallel.ForEach(extensionElems, extensionElem => {
		        var supported = extensionElem.Attribute("supported")?.Value;
		        if (supported == "disabled")
			        return;

		        var name = extensionElem.Attribute("name")?.Value;
		        var type = extensionElem.Attribute("type")?.Value;

		        switch (type) {
			        default:
				        throw new NotImplementedException(type);
			        case "instance":
				        InstanceExtensions.Add(name, extensionElem);
				        return;
			        case "device":
				        DeviceExtensions.Add(name, extensionElem);
				        return;
		        }
	        });

            var enumsElems = registryElem.Elements("enums");

	        Parallel.ForEach(enumsElems, enumsElem => {
		        var name = enumsElem.Attribute("name")?.Value;
		        var type = enumsElem.Attribute("type")?.Value;

		        switch (type) {
			        case null:
				        if (name != "API Constants")
					        throw new NotSupportedException("Encountered untyped enumeration element.");
				        Parallel.ForEach(enumsElem.Elements("enum"), e
					        => Constants.Add(new KeyValuePair<string, string>(
						        e.Attribute("name")?.Value
						        ?? throw new NotSupportedException("Encountered constant enumeration member without a name attribute."),
						        e.Attribute("value")?.Value
					        )));
				        return;
			        default:
				        throw new NotImplementedException(type);
			        case "enum":
				        Enums.Add(name, enumsElem.Elements("enum")
					        .ToDictionary(
						        e => e.Attribute("name")?.Value
									?? throw new NotSupportedException("Encountered enumeration member without a name attribute."),
						        e => e.Attribute("value")?.Value
					        ));
				        return;
			        case "bitmask":
				        Bitmasks.Add(name, enumsElem.Elements("enum")
					        .ToDictionary(
						        e => e.Attribute("name")?.Value
									?? throw new NotSupportedException("Encountered enumeration member without a name attribute."),
						        e => {
							        var bitpos = e.Attribute("bitpos")?.Value;
							        var value = bitpos == null
								        ? ParseIntegerString(e.Attribute("value")?.Value)
								        : 1 << int.Parse(bitpos);
							        return value;
						        }
					        ));
				        return;
		        }
	        });
			
	        MakeImmutable(ref Constants);
	        MakeImmutable(ref UncategorizedTypes);
	        MakeImmutable(ref DefineTypes);
	        MakeImmutable(ref StructTypes);
	        MakeImmutable(ref FunctionPointerTypes);
	        MakeImmutable(ref HandleTypes);
	        MakeImmutable(ref BitmaskTypes);
	        MakeImmutable(ref EnumTypes);
	        MakeImmutable(ref UnionTypes);
	        MakeImmutable(ref IncludeTypes);
	        MakeImmutable(ref BaseTypeTypes);
	        MakeImmutable(ref Commands);
	        MakeImmutable(ref InstanceExtensions);
	        MakeImmutable(ref DeviceExtensions);
	        MakeImmutable(ref Enums);
	        MakeImmutable(ref Bitmasks);
        }

	    private static void MakeImmutable<TKey, TValue>(ref IDictionary<TKey, TValue> dictionary)
			=> dictionary = dictionary as ImmutableDictionary<TKey,TValue>
			?? dictionary.ToImmutableDictionary();

		private int ParseIntegerString(string value) {
            if (value.StartsWith("0x"))
                return int.Parse(value.Substring(2), NumberStyles.HexNumber);
            if (value.StartsWith("-0x"))
                return -int.Parse(value.Substring(3), NumberStyles.HexNumber);
            return Int32.Parse(value, NumberStyles.Integer);
        }
    }
}