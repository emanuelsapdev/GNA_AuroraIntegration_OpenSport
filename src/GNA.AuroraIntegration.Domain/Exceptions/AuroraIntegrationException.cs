namespace GNA.AuroraIntegration.Domain.Exceptions;

public class AuroraIntegrationException : Exception
{
    public AuroraIntegrationException(string message) : base(message) { }
    public AuroraIntegrationException(string message, Exception inner) : base(message, inner) { }
}