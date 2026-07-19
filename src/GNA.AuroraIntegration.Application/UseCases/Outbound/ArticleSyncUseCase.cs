using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound;

public interface IArticleSyncUseCase
{
    Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default);
}

/// <summary>
/// Caso de uso: toma artículos pendientes de SAP B1 y los envía a Aurora WMS.
/// </summary>
public sealed class ArticleSyncUseCase : IArticleSyncUseCase
{
    private readonly IArticleReplicationRepository _repository;
    private readonly IAuroraArticleApiClient _auroraClient;
    private readonly ILogger<ArticleSyncUseCase> _logger;

    public ArticleSyncUseCase(
        IArticleReplicationRepository repository,
        IAuroraArticleApiClient auroraClient,
        ILogger<ArticleSyncUseCase> logger)
    {
        _repository = repository;
        _auroraClient = auroraClient;
        _logger = logger;
    }

    public async Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default)
    {
        int processed = 0, successful = 0, failed = 0;

        try
        {
            var pending = await _repository.GetPendingArticlesAsync(batchSize: 100, ct);

            foreach (var article in pending)
            {
                processed++;
                ct.ThrowIfCancellationRequested();
                try
                {
                    var dto = new CreateArticleDto
                    {
                        Sku = article.Sku,
                        Name = article.Name,
                        Ean = article.PrimaryEan,
                        WeightInGr = (double)article.WeightInGr,
                        HeightInCm = (double)article.HeightInCm,
                        WidthInCm = (double)article.WidthInCm,
                        LengthInCm = (double)article.LengthInCm
                    };

                    await _auroraClient.CreateArticleAsync(dto, warehouse: null, ct);
                    await _repository.MarkArticleAsReplicatedAsync(article.Sku, ct);
                    
                    successful++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replicando artículo '{Sku}'", article.Sku);
                    
                    await _repository.MarkArticleAsFailedAsync(article.Sku, ex.Message, ct);
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener artículos pendientes del repositorio");
            
        }

        return (processed, successful, failed);
    }
}