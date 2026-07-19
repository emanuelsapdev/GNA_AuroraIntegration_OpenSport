namespace GNA.AuroraIntegration.Domain.Exceptions;

public sealed class UseCaseValidationException : AuroraIntegrationException
{
    public UseCaseValidationException(string message)
        : base(message)
    {
    }
}
