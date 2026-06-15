namespace Application.Dto.WorkPackages;

public record CustomFieldOption(int Id, string Value);

public record RequiredCustomField(string Key, string Name, List<CustomFieldOption> AllowedValues);
