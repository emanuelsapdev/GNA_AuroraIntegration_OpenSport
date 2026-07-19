using GNA.AuroraIntegration.Application.DTOs.Aurora;

namespace GNA.AuroraIntegration.Application.Interfaces;

/// <summary>
/// Contrato hacia la API REST de Aurora WMS. La implementación concreta
/// (HttpClient tipado) vive en Infrastructure/Aurora.
/// </summary>
public interface IAuroraArticleApiClient
{
    Task CreateArticleAsync(CreateAuroraArticleDto article, string? warehouse, CancellationToken ct = default);
    Task UpdateArticleAsync(string sku, UpdateAuroraArticleDto article, string? warehouse, CancellationToken ct = default);
    Task<AuroraArticleDto?> GetArticleBySkuAsync(string sku, string? warehouse, CancellationToken ct = default);
    // ... resto de métodos según se agreguen use cases (CreateSaleOrder, GetStock, etc.)
}