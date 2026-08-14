namespace Application.Dto.Tasks;

/// <summary>
/// <paramref name="UploadNow"/>: true sube el tiempo pendiente a OpenProject al pausar;
/// false lo deja guardado en local (Uploaded = false) para subirlo al retomar y terminar
/// la tarea. La sesión se cierra igual en ambos casos — nunca queda con EndTime nulo.
/// </summary>
public record PauseTaskRequest(int WorkPackageId, int? OnHoldStatusId = null, bool UploadNow = true);
