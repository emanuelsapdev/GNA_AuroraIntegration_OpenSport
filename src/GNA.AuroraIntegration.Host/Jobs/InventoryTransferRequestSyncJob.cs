using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using Quartz;

namespace GNA.AuroraIntegration.Host.Jobs;

[DisallowConcurrentExecution]
public sealed class InventoryTransferRequestSyncJob : IJob
{
    private readonly IInventoryTransferRequestSyncUseCase _useCase;
    private readonly ILogger<InventoryTransferRequestSyncJob> _logger;

    public InventoryTransferRequestSyncJob(IInventoryTransferRequestSyncUseCase useCase, ILogger<InventoryTransferRequestSyncJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("InventoryTransferRequestSyncJob iniciado");
        var count = await _useCase.ExecuteAsync(context.CancellationToken);
        _logger.LogInformation("InventoryTransferRequestSyncJob finalizado. Procesados: {Count}", count);
    }
}
