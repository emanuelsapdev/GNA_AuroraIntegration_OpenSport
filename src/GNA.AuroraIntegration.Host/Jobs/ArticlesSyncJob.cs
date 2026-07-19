using GNA.AuroraIntegration.Application.UseCases.Outbound;
using Quartz;

namespace GNA.AuroraIntegration.Host.Jobs;

[DisallowConcurrentExecution]
public sealed class ArticlesSyncJob : IJob
{
    private readonly IArticleSyncUseCase _useCase;
    private readonly ILogger<ArticlesSyncJob> _logger;

    public ArticlesSyncJob(IArticleSyncUseCase useCase, ILogger<ArticlesSyncJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("ArticlesSyncJob iniciado");
        var count = await _useCase.ExecuteAsync(context.CancellationToken);
        _logger.LogInformation("ArticlesSyncJob finalizado. Procesados: {Count}", count);
    }
}