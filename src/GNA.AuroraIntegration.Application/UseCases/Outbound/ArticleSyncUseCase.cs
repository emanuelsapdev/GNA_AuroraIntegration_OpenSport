using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
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
                    var exists = await _auroraClient.GetArticleBySkuAsync(article.Sku, null, ct);
                    if (exists is null)
                    {
                        var dtoCreate = MapperToCreateArticle(article);
                        await _auroraClient.CreateArticleAsync(dtoCreate, warehouse: null, ct);
                    } else
                    {
                        var dtoUpdate = MapperToUpdateArticle(article);
                        await _auroraClient.UpdateArticleAsync(article.Sku, dtoUpdate, warehouse: null, ct);
                    }
                    await _repository.MarkArticleAsReplicatedAsync(article.Sku, ct);
                    successful++;
                }
                catch (Exception ex) // "Error de conexión al registrar intento exitoso de 'Item1' en SAP B1."
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


    // ---- mappers ----
    private CreateAuroraArticleDto MapperToCreateArticle(Article article) => new CreateAuroraArticleDto
    {
        Sku = article.Sku,
        Name = article.Name ?? string.Empty,
        Ean = article.PrimaryEan ?? string.Empty,
        Eans = article.AdditionalEans?.ToArray(),
        BannerName = string.Empty, // TODO: mapear el nombre del banner si es necesario
        BannerExternalId = string.Empty, // TODO: mapear el ID externo del banner si es necesario
        WeightInGr = (double?)article.WeightInGr,
        HeightInCm = (double?)article.HeightInCm,
        WidthInCm = (double?)article.WidthInCm,
        LengthInCm = (double?)article.LengthInCm,
        IsConsumable = article.IsConsumable,
        HasProductionBatch = article.HasProductionBatch,
        HasDueDate = article.HasDueDate,
        HasSerialNumber = article.HasSerialNumber,
        Colour = article.Colour,
        Bulky = false, // TODO: mapear si es voluminoso si es necesario
        Cage = false, // TODO: mapear la jaula si es necesario
        Size = article.Size
    };

    private UpdateAuroraArticleDto MapperToUpdateArticle(Article article) => new UpdateAuroraArticleDto
    {
        Name = article.Name ?? string.Empty,
        Ean = article.PrimaryEan ?? string.Empty,
        Eans = article.AdditionalEans?.ToArray(),
        BannerName = string.Empty, // TODO: mapear el nombre del banner si es necesario
        BannerExternalId = string.Empty, // TODO: mapear el ID externo del banner si es necesario
        WeightInGr = (double?)article.WeightInGr,
        HeightInCm = (double?)article.HeightInCm,
        WidthInCm = (double?)article.WidthInCm,
        LengthInCm = (double?)article.LengthInCm,
        IsConsumable = article.IsConsumable,
        HasProductionBatch = article.HasProductionBatch,
        HasDueDate = article.HasDueDate,
        HasSerialNumber = article.HasSerialNumber,
        Colour = article.Colour,
        Bulky = false, // TODO: mapear si es voluminoso si es necesario
        Cage = false, // TODO: mapear la jaula si es necesario
        Size = article.Size,
        
    };
    
}