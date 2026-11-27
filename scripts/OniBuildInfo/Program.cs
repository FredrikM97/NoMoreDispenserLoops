using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: OniBuildInfo ASSEMBLY_PATH OUTPUT_PATH");
    return 1;
}

using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
MetadataReader metadata = pe.GetMetadataReader();
foreach (TypeDefinitionHandle typeHandle in metadata.TypeDefinitions)
{
    TypeDefinition type = metadata.GetTypeDefinition(typeHandle);
    if (metadata.GetString(type.Name) != "KleiVersion" || !type.Namespace.IsNil)
        continue;

    foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
    {
        FieldDefinition field = metadata.GetFieldDefinition(fieldHandle);
        if (metadata.GetString(field.Name) != "ChangeList")
            continue;

        ConstantHandle constantHandle = field.GetDefaultValue();
        if (constantHandle.IsNil)
            break;
        Constant constant = metadata.GetConstant(constantHandle);
        if (constant.TypeCode != ConstantTypeCode.UInt32)
            break;

        uint build = metadata.GetBlobReader(constant.Value).ReadUInt32();
        File.WriteAllText(args[1], build.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
        return 0;
    }
}

Console.Error.WriteLine($"Could not read KleiVersion.ChangeList from {args[0]}.");
return 1;
