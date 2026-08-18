namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// Error al replicar una Solicitud de Traslado hacia Aurora (falla de alta vía API).
/// </summary>
public sealed class InventoryTransferRequestAuroraApiException : AuroraIntegrationException
{
    public string DocEntry { get; }

    public InventoryTransferRequestAuroraApiException(string docEntry, string message)
        : base(message)
    {
        DocEntry = docEntry;
    }

    public InventoryTransferRequestAuroraApiException(string docEntry, string message, Exception inner)
        : base(message, inner)
    {
        DocEntry = docEntry;
    }
}
