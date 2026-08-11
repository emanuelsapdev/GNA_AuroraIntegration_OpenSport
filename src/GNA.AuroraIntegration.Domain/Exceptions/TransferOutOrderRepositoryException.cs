namespace GNA.AuroraIntegration.Domain.Exceptions;

public sealed class TransferOutOrderRepositoryException : AuroraIntegrationException
{
    public TransferOutOrderRepositoryException(string message)
        : base(message) { }

    public TransferOutOrderRepositoryException(string message, Exception inner)
        : base(message, inner) { }
}
