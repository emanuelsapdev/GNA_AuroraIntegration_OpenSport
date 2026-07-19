namespace GNA.AuroraIntegration.Application.UseCases.Outbound;

public interface IArticleSyncUseCase
{
    Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default);
}
