namespace GNA.AuroraIntegration.Application.UseCases;

public interface IEnsureReplicationSchemaUseCase
{
    Task ExecuteAsync(CancellationToken ct = default);
}
