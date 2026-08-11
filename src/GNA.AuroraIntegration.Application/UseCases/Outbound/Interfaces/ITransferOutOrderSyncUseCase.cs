namespace GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;

public interface ITransferOutOrderSyncUseCase
{
    Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default);
}
