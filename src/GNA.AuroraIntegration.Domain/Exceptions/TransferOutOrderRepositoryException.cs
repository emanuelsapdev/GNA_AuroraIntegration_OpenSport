namespace GNA.AuroraIntegration.Domain.Exceptions;

public sealed class InventoryTransferRequestRepositoryException : AuroraIntegrationException
{
    public InventoryTransferRequestRepositoryException(string message)
        : base(message) { }

    public InventoryTransferRequestRepositoryException(string message, Exception inner)
        : base(message, inner) { }
}
