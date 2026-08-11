using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound;

/// <summary>
/// Caso de uso: toma artículos pendientes de SAP B1 y los envía a Aurora WMS.
/// </summary>
public sealed class ArticleSyncUseCase : IArticleSyncUseCase
{
    private readonly IArticleReplicationRepository _repository;
    private readonly IAuroraArticleApiClient _auroraClient;
    private readonly IArticlePayloadValidator _validator;
    private readonly ILogger<ArticleSyncUseCase> _logger;

    public ArticleSyncUseCase(
        IArticleReplicationRepository repository,
        IAuroraArticleApiClient auroraClient,
        IArticlePayloadValidator validator,
        ILogger<ArticleSyncUseCase> logger)
    {
        _repository = repository;
        _auroraClient = auroraClient;
        _validator = validator;
        _logger = logger;
    }

    public async Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default)
    {
        int processed = 0;
        int successful = 0;
        int failed = 0;

        try
        {
            IReadOnlyList<Article> pending = await _repository.GetPendingArticlesAsync(batchSize: 100, ct);

            foreach (Article article in pending)
            {
                processed++;
                ct.ThrowIfCancellationRequested();

                try
                {
                    AuroraArticleDto? existingArticle = await _auroraClient.GetArticleBySkuAsync(article.Sku, warehouse: null, ct);

                    if (existingArticle is null)
                    {
                        CreateAuroraArticleDto createDto = MapToCreateArticle(article);
                        _validator.Validate(createDto);
                        await _auroraClient.CreateArticleAsync(createDto, warehouse: null, ct);
                        _logger.LogInformation("Artículo '{Sku}' creado en Aurora.", article.Sku);
                    }
                    else
                    {
                        UpdateAuroraArticleDto updateDto = MapToUpdateArticle(article);
                        _validator.Validate(updateDto);
                        await _auroraClient.UpdateArticleAsync(article.Sku, updateDto, warehouse: null, ct);
                        _logger.LogInformation("Artículo '{Sku}' actualizado en Aurora.", article.Sku);
                    }

                    await _repository.MarkArticleAsReplicatedAsync(article.Sku, ct);
                    successful++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replicando artículo '{Sku}'", article.Sku);
                    await _repository.MarkArticleAsFailedAsync(article.Sku, ex.Message, ct);
                    failed++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener artículos pendientes del repositorio");
            throw;
        }

        return (processed, successful, failed);
    }

    private static string[]? MapEans(IReadOnlyList<string> eans)
        => eans.Count == 0 ? null : [.. eans];

    private static CreateAuroraArticleDto MapToCreateArticle(Article article) => new()
    {
        Sku = article.Sku,
        Name = article.Name,
        Ean = article.PrimaryEan,
        Eans = MapEans(article.AdditionalEans),
        BrandExternalId = article.BrandID,
        BrandName = article.BrandName,
        CategoryName = article.CategoryName,
        BannerExternalId = article.BannerID,
        BannerName = article.BannerName,
        Bulky = article.IsBulky,
        Cage = article.IsCaged
    };

    private static UpdateAuroraArticleDto MapToUpdateArticle(Article article) => new()
    {
        Sku = article.Sku,
        Name = article.Name,
        Eans = MapEans(article.AdditionalEans),
        BrandExternalId = article.BrandID,
        BrandName = article.BrandName,
        CategoryName = article.CategoryName,
        BannerExternalId = article.BannerID,
        BannerName = article.BannerName,
        Bulky = article.IsBulky,
        Cage = article.IsCaged
    };
}
