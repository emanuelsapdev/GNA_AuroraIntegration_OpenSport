using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.DTOs;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;
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
        var item = await _client.GetAsync<ServiceLayerItemDto>($"{SapB1ItemsConstants.Items.Endpoint}('{escapedSku}')", ct);

        if (item is null)
            return null;

        // U_GNA_AUR_Marca y U_GNA_AUR_Banner son LinkedTable → OITM solo guarda el Code
        // de GNA_AUR_MARCAS/GNA_AUR_BANNERS. Resolvemos el Name con una consulta adicional
        // por tabla (batch de 1 elemento cada una).
        Dictionary<string, string> brandNamesByCode = await GetNamesByCodeAsync(
            SapB1ProductBrandsConstants.ProductBrands.Endpoint,
            SapB1ProductBrandsConstants.ProductBrands.CodeField,
            SapB1ProductBrandsConstants.ProductBrands.NameField,
            SingleCodeOrEmpty(item.BrandID), "Marcas (GNA_AUR_MARCAS)", ct);

        Dictionary<string, string> bannerNamesByCode = await GetNamesByCodeAsync(
            SapB1BannersConstants.Banners.Endpoint,
            SapB1BannersConstants.Banners.CodeField,
            SapB1BannersConstants.Banners.NameField,
            SingleCodeOrEmpty(item.BannerID), "Banners (GNA_AUR_BANNERS)", ct);

        return MapToArticle(item, brandNamesByCode, bannerNamesByCode);
    }

    public async Task<IReadOnlyList<Article>> GetBySkuListAsync(
        IEnumerable<string> skus, CancellationToken ct = default)
    {
        var skuList = skus.Distinct().ToList();
        if (skuList.Count == 0)
            return Array.Empty<Article>();

        var items = new List<ServiceLayerItemDto>(skuList.Count);

        foreach (var batch in Chunk(skuList, FilterBatchSize))
        {
            var filter = string.Join(" or ",
                batch.Select(sku => $"ItemCode eq '{EscapeODataValue(sku)}'"));

            string fields = $"{SapB1ItemsConstants.Items.ItemCodeField}," +
                            $"{SapB1ItemsConstants.Items.ItemNameField}," +
                            $"{SapB1ItemsConstants.Items.BarCodeField}," +
                            $"{SapB1ItemsConstants.Items.BannerField}," +
                            $"{SapB1ItemsConstants.Items.ProductBrandField}," +
                            $"{SapB1ItemsConstants.Items.LogisticsCategoryField}," +
                            $"{SapB1ItemsConstants.Items.IsBulkyField}," +
                            $"{SapB1ItemsConstants.Items.IsCagedField}";

            var resource = $"{SapB1ItemsConstants.Items.Endpoint}?$filter={filter}" +
                            $"&$select={fields}";

            var response = await _client.GetAsync<ServiceLayerCollectionDto<ServiceLayerItemDto>>(resource, ct);

            if (response?.Value.Count == 0)
            {
                _logger.LogWarning("Consulta de Items en Service Layer no devolvió resultados para el lote actual.");
                continue;
            }

            items.AddRange(response!.Value);
        }

        // Resolución de Name de Marca/Banner: UNA sola consulta batcheada por tabla, por
        // todos los Codes distintos del lote completo (no una consulta por artículo — ver
        // nota en SapB1ProductBrandsConstants/SapB1BannersConstants). Service Layer no
        // soporta $expand para UDFs LinkedTable, así que este es el equivalente más cercano
        // a un "join" posible.
        Dictionary<string, string> brandNamesByCode = await GetNamesByCodeAsync(
            SapB1ProductBrandsConstants.ProductBrands.Endpoint,
            SapB1ProductBrandsConstants.ProductBrands.CodeField,
            SapB1ProductBrandsConstants.ProductBrands.NameField,
            DistinctCodes(items, i => i.BrandID), "Marcas (GNA_AUR_MARCAS)", ct);

        Dictionary<string, string> bannerNamesByCode = await GetNamesByCodeAsync(
            SapB1BannersConstants.Banners.Endpoint,
            SapB1BannersConstants.Banners.CodeField,
            SapB1BannersConstants.Banners.NameField,
            DistinctCodes(items, i => i.BannerID), "Banners (GNA_AUR_BANNERS)", ct);

        return items.Select(item => MapToArticle(item, brandNamesByCode, bannerNamesByCode)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Resuelve Code → Name para una UDT tipo MasterData (Marcas o Banners), en lotes de
    /// <see cref="FilterBatchSize"/> usando el mismo patrón de $filter "or" que
    /// GetBySkuListAsync. Es la única forma disponible de resolver el Name: Service Layer
    /// no expone las UDFs LinkedTable como navigation properties OData, por lo que no
    /// existe un $expand/cross-join de una sola query que traiga Items + Name en la misma
    /// llamada — se paga como una consulta adicional batcheada por tabla, por corrida.
    /// </summary>
    private async Task<Dictionary<string, string>> GetNamesByCodeAsync(
        string endpoint, string codeField, string nameField,
        IReadOnlyCollection<string> codes, string logLabel, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (codes.Count == 0)
            return result;

        foreach (var batch in Chunk(codes.ToList(), FilterBatchSize))
        {
            var filter = string.Join(" or ",
                batch.Select(code => $"{codeField} eq '{EscapeODataValue(code)}'"));

            string fields = $"{codeField},{nameField}";

            var resource = $"{endpoint}?$filter={filter}&$select={fields}";

            var response = await _client.GetAsync<ServiceLayerCollectionDto<ServiceLayerCodeNameDto>>(resource, ct);

            if (response?.Value.Count == 0)
            {
                _logger.LogWarning("Consulta de {Label} en Service Layer no devolvió resultados para el lote actual.", logLabel);
                continue;
            }

            foreach (var row in response!.Value)
                result[row.Code] = row.Name;
        }

        return result;
    }

    private static List<string> SingleCodeOrEmpty(string? code)
        => string.IsNullOrWhiteSpace(code) ? [] : [code];

    private static List<string> DistinctCodes(List<ServiceLayerItemDto> items, Func<ServiceLayerItemDto, string?> selector)
        => items
            .Select(selector)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Traduce el DTO de Service Layer a la entidad de dominio Article.
    /// </summary>
    private static Article MapToArticle(
        ServiceLayerItemDto dto,
        IReadOnlyDictionary<string, string> brandNamesByCode,
        IReadOnlyDictionary<string, string> bannerNamesByCode)
        => new Article {
            Sku = dto.ItemCode,
            Name = dto.ItemName,
            PrimaryEan = dto.BarCode,
            BannerID = dto.BannerID,
            BannerName = ResolveName(dto.BannerID, bannerNamesByCode),
            BrandID = dto.BrandID,
            BrandName = ResolveName(dto.BrandID, brandNamesByCode),
            CategoryName = dto.CategoryName,
            IsBulky = dto.IsBulky == "Y",
            IsCaged = dto.IsCaged == "Y"
        };

    private static string? ResolveName(string? code, IReadOnlyDictionary<string, string> namesByCode)
        => !string.IsNullOrWhiteSpace(code) && namesByCode.TryGetValue(code, out var name) ? name : null;

    private static string EscapeODataValue(string value) => value.Replace("'", "''");

    private static IEnumerable<List<string>> Chunk(List<string> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}

/// <summary>
/// DTO interno del recurso Items de Service Layer (subset de campos usados).
/// ⚠️ ServiceLayerClient deserializa con PropertyNamingPolicy = null (match exacto y
/// case-sensitive) — por eso cada UDF (prefijo "U_") requiere [JsonPropertyName] explícito;
/// sin esto el campo queda silenciosamente en null aunque Service Layer sí lo devuelva
/// (bug preexistente detectado al corregir SapB1ItemsConstants para este ticket).
/// </summary>
internal sealed class ServiceLayerItemDto
{
    public string ItemCode { get; set; } = default!;
    public string ItemName { get; set; } = default!;
    public string BarCode { get; set; } = default!;

    [JsonPropertyName(SapB1ItemsConstants.Items.BannerField)]
    public string? BannerID { get; set; }

    [JsonPropertyName(SapB1ItemsConstants.Items.ProductBrandField)]
    public string? BrandID { get; set; }

    [JsonPropertyName(SapB1ItemsConstants.Items.LogisticsCategoryField)]
    public string? CategoryName { get; set; }

    [JsonPropertyName(SapB1ItemsConstants.Items.IsBulkyField)]
    public string? IsBulky { get; set; }

    [JsonPropertyName(SapB1ItemsConstants.Items.IsCagedField)]
    public string? IsCaged { get; set; }
}

/// <summary>
/// DTO interno genérico Code/Name, reutilizado para los recursos U_GNA_AUR_MARCAS y
/// U_GNA_AUR_BANNERS de Service Layer (ambos son UDTs MasterData con la misma forma:
/// Code/Name son campos estándar, sin prefijo "U_").
/// </summary>
internal sealed class ServiceLayerCodeNameDto
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
}
