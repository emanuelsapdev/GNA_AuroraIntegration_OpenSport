using GNA.AuroraIntegration.Application.DTOs.Aurora;

namespace GNA.AuroraIntegration.Application.Interfaces;

public interface IAuroraArticleApiClient
{
    Task CreateArticleAsync(CreateAuroraArticleDto article, string? warehouse, CancellationToken ct = default);
    Task UpdateArticleAsync(string sku, UpdateAuroraArticleDto article, string? warehouse, CancellationToken ct = default);
    Task<AuroraArticleDto?> GetArticleBySkuAsync(string sku, string? warehouse, CancellationToken ct = default);
}
