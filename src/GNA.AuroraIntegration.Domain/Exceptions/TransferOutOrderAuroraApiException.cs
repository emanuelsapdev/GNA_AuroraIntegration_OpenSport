namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// Error al replicar una Solicitud de Traslado hacia Aurora (falla de alta vía API).
/// </summary>
public sealed class TransferOutOrderAuroraApiException : AuroraIntegrationException
{
    public string DocEntry { get; }

    public TransferOutOrderAuroraApiException(string docEntry, string message)
        : base(message)
    {
        DocEntry = docEntry;
    }

    public TransferOutOrderAuroraApiException(string docEntry, string message, Exception inner)
        : base(message, inner)
    {
        DocEntry = docEntry;
    }
}
