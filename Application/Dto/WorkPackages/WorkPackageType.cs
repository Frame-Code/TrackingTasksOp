namespace Application.Dto.WorkPackages;

/// <summary>
/// Tipo de work package configurado en un proyecto de OpenProject
/// (ej. "DESARROLLO", "SOPORTE TECNICO", "ERROR"). Los tipos y sus IDs
/// varían por proyecto e instancia, por eso siempre se consultan.
/// </summary>
public record WorkPackageType(int Id, string Name);
