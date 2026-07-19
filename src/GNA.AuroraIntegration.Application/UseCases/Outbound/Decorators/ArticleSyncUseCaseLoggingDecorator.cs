using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound.Decorators;

public sealed class ArticleSyncUseCaseLoggingDecorator : IArticleSyncUseCase
{
    private readonly IArticleSyncUseCase _inner;
    private readonly ILogger<ArticleSyncUseCaseLoggingDecorator> _logger;

    public ArticleSyncUseCaseLoggingDecorator(
        IArticleSyncUseCase inner,
        ILogger<ArticleSyncUseCaseLoggingDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando pipeline de sincronización de artículos.");

        (int processed, int successful, int failed) result = await _inner.ExecuteAsync(ct);

        _logger.LogInformation(
            "Pipeline de sincronización finalizado. Procesados: {Processed}, exitosos: {Successful}, fallidos: {Failed}.",
            result.processed,
            result.successful,
            result.failed);

        return result;
    }
}
