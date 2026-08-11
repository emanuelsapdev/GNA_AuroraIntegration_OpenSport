using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound.Decorators;

public sealed class TransferOutOrderSyncUseCaseLoggingDecorator : ITransferOutOrderSyncUseCase
{
    private readonly ITransferOutOrderSyncUseCase _inner;
    private readonly ILogger<TransferOutOrderSyncUseCaseLoggingDecorator> _logger;

    public TransferOutOrderSyncUseCaseLoggingDecorator(
        ITransferOutOrderSyncUseCase inner,
        ILogger<TransferOutOrderSyncUseCaseLoggingDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando pipeline de sincronización de órdenes de transferencia de salida.");

        (int processed, int successful, int failed) result = await _inner.ExecuteAsync(ct);

        _logger.LogInformation(
            "Pipeline de sincronización de órdenes de transferencia de salida finalizado. Procesadas: {Processed}, exitosas: {Successful}, fallidas: {Failed}.",
            result.processed,
            result.successful,
            result.failed);

        return result;
    }
}
