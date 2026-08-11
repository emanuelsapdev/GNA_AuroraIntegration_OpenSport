namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// La Orden de Compra esperada no fue encontrada en la fuente de datos correspondiente.
/// </summary>
public sealed class PurchaseOrderNotFoundException : AuroraIntegrationException
{
    public string DocEntry { get; }

    public PurchaseOrderNotFoundException(string docEntry)
        : base($"No se encontró la Orden de Compra con DocEntry '{docEntry}'.")
    {
        DocEntry = docEntry;
    }
}
