using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using Quartz;

namespace GNA.AuroraIntegration.Host.Jobs;

[DisallowConcurrentExecution]
public sealed class TransferOutOrdersSyncJob : IJob
{
    private readonly ITransferOutOrderSyncUseCase _useCase;
    private readonly ILogger<TransferOutOrdersSyncJob> _logger;

    public TransferOutOrdersSyncJob(ITransferOutOrderSyncUseCase useCase, ILogger<TransferOutOrdersSyncJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("TransferOutOrdersSyncJob iniciado");
        var count = await _useCase.ExecuteAsync(context.CancellationToken);
        _logger.LogInformation("TransferOutOrdersSyncJob finalizado. Procesados: {Count}", count);
    }
}
