using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using Quartz;

namespace GNA.AuroraIntegration.Host.Jobs;

[DisallowConcurrentExecution]
public sealed class PurchaseOrdersSyncJob : IJob
{
    private readonly IPurchaseOrderSyncUseCase _useCase;
    private readonly ILogger<PurchaseOrdersSyncJob> _logger;

    public PurchaseOrdersSyncJob(IPurchaseOrderSyncUseCase useCase, ILogger<PurchaseOrdersSyncJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("PurchaseOrdersSyncJob iniciado");
        var count = await _useCase.ExecuteAsync(context.CancellationToken);
        _logger.LogInformation("PurchaseOrdersSyncJob finalizado. Procesados: {Count}", count);
    }
}
