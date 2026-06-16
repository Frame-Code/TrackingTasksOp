namespace Application.Dto.WorkPackages;

public record CustomFieldOption(int Id, string Value);

/// <summary>
/// FieldType: "CustomOption" para listas desplegables (se envían como links),
/// cualquier otro valor ("String", "Integer", etc.) para campos de texto libre
/// (se envían como propiedades directas en el payload).
/// </summary>
public record RequiredCustomField(string Key, string Name, string FieldType, List<CustomFieldOption> AllowedValues)
{
    public bool IsOptionType => FieldType == "CustomOption";
}
