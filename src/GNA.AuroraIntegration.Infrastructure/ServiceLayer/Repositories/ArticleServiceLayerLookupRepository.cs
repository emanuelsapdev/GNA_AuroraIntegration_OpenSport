using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Repositories;

/// <summary>
/// Implementa IArticleLookupRepository consultando el recurso Items de
/// SAP B1 Service Layer. Fuente de verdad de negocio para Article antes
/// de replicar hacia Aurora.
/// </summary>
public sealed class ArticleServiceLayerLookupRepository : IArticleLookupRepository
{
    // Tamaño de sub-lote para $filter con múltiples "or" — evita URLs demasiado largas.
    private const int FilterBatchSize = 20;

    private readonly IServiceLayerClient _client;
    private readonly ILogger<ArticleServiceLayerLookupRepository> _logger;

    public ArticleServiceLayerLookupRepository(
        IServiceLayerClient client,
        ILogger<ArticleServiceLayerLookupRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Article?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var escapedSku = EscapeODataValue(sku);
        var item = await _client.GetAsync<ServiceLayerItemDto>($"Items('{escapedSku}')", ct);

        return item is null ? null : MapToArticle(item);
    }

    public async Task<IReadOnlyList<Article>> GetBySkuListAsync(
        IEnumerable<string> skus, CancellationToken ct = default)
    {
        var skuList = skus.Distinct().ToList();
        if (skuList.Count == 0)
            return Array.Empty<Article>();

        var result = new List<Article>(skuList.Count);

        foreach (var batch in Chunk(skuList, FilterBatchSize))
        {
            var filter = string.Join(" or ",
                batch.Select(sku => $"ItemCode eq '{EscapeODataValue(sku)}'"));

            var resource = $"Items?$filter={filter}" +
                            "&$select=ItemCode,ItemName,BarCode";

            var response = await _client.GetAsync<ServiceLayerCollectionDto<ServiceLayerItemDto>>(resource, ct);

            if (response?.Value.Count == 0)
            {
                _logger.LogWarning("Consulta de Items en Service Layer no devolvió resultados para el lote actual.");
                continue;
            }

            result.AddRange(response.Value.Select(MapToArticle));
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Traduce el DTO de Service Layer a la entidad de dominio Article.
    /// ⚠️ Ajustar según la forma real de Article.cs.
    /// </summary>
    private static Article MapToArticle(ServiceLayerItemDto dto)
        => new Article { Sku = dto.ItemCode, Name = dto.ItemName, PrimaryEan = dto.BarCode };

    private static string EscapeODataValue(string value) => value.Replace("'", "''");

    private static IEnumerable<List<string>> Chunk(List<string> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}

/// <summary>DTO interno del recurso Items de Service Layer (subset de campos usados).</summary>
internal sealed class ServiceLayerItemDto
{
    public string ItemCode { get; set; } = default!;
    public string ItemName { get; set; } = default!;
    public string? BarCode { get; set; }
}

/// <summary>Envoltorio genérico de colección OData de Service Layer.</summary>
internal sealed class ServiceLayerCollectionDto<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
}
