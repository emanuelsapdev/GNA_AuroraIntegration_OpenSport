namespace GNA.AuroraIntegration.Domain.Exceptions;

public sealed class PurchaseOrderRepositoryException : AuroraIntegrationException
{
    public PurchaseOrderRepositoryException(string message)
        : base(message) { }

    public PurchaseOrderRepositoryException(string message, Exception inner)
        : base(message, inner) { }
}
