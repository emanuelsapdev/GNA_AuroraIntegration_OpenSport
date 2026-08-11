namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// La Solicitud de Traslado esperada no fue encontrada en la fuente de datos correspondiente.
/// </summary>
public sealed class TransferOutOrderNotFoundException : AuroraIntegrationException
{
    public string DocEntry { get; }

    public TransferOutOrderNotFoundException(string docEntry)
        : base($"No se encontró la Solicitud de Traslado con DocEntry '{docEntry}'.")
    {
        DocEntry = docEntry;
    }
}
