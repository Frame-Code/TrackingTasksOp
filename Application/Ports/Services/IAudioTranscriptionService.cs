namespace Application.Ports.Services;

/// <summary>
/// Define el contrato para un servicio que transcribe audio a texto (Speech-to-Text),
/// para alimentar el mismo pipeline de <see cref="IAiIntentService"/> con el texto resultante.
/// </summary>
public interface IAudioTranscriptionService
{
    /// <summary>
    /// Indica si hay credenciales configuradas para realizar transcripciones.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Transcribe un audio a texto.
    /// </summary>
    /// <param name="audio">Stream con el contenido del archivo de audio.</param>
    /// <param name="fileName">Nombre del archivo (usado para inferir el formato).</param>
    /// <param name="contentType">Content-Type del archivo (ej. "audio/webm").</param>
    /// <param name="ct">Cancellation Token.</param>
    /// <returns>El texto transcrito.</returns>
    Task<string> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct = default);
}
