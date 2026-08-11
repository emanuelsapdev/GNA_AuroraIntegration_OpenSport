namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// Error al replicar una Orden de Compra hacia Aurora (falla de alta vía API).
/// </summary>
public sealed class PurchaseOrderAuroraApiException : AuroraIntegrationException
{
    public string DocEntry { get; }

    public PurchaseOrderAuroraApiException(string docEntry, string message)
        : base(message)
    {
        DocEntry = docEntry;
    }

    public PurchaseOrderAuroraApiException(string docEntry, string message, Exception inner)
        : base(message, inner)
    {
        DocEntry = docEntry;
    }
}
