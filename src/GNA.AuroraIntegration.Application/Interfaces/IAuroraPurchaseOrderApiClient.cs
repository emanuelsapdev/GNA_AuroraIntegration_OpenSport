using GNA.AuroraIntegration.Application.DTOs.Aurora.PurchaseOrder;

namespace GNA.AuroraIntegration.Application.Interfaces;

public interface IAuroraPurchaseOrderApiClient
{
    Task CreatePurchaseOrderAsync(CreateAuroraPurchaseOrderDto purchaseOrder, string? warehouse, CancellationToken ct = default);
    Task<AuroraPurchaseOrderDto?> GetPurchaseOrderByExternalIdAsync(string externalId, string? warehouse, CancellationToken ct = default);

    /// <summary>Cancela (DELETE) una OC existente en Aurora. Idempotente: no falla si ya no existe.</summary>
    Task CancelPurchaseOrderAsync(string externalId, string? warehouse, CancellationToken ct = default);

    /// <summary>Estado actual de las líneas de una OC en Aurora (cantidad solicitada vs. cumplida).</summary>
    Task<IReadOnlyList<PurchaseOrderArticleStateDto>> GetPurchaseOrderArticlesAsync(string externalId, string? warehouse, CancellationToken ct = default);

    /// <summary>Agrega una o más líneas nuevas a una OC ya existente en Aurora.</summary>
    Task AddPurchaseOrderArticlesAsync(string externalId, IReadOnlyList<PurchaseOrderArticleDto> articles, string? warehouse, CancellationToken ct = default);

    /// <summary>Edita cantidad/orden de una línea existente de la OC en Aurora.</summary>
    Task UpdatePurchaseOrderArticleAsync(string externalId, string articleSku, PurchaseOrderArticleDto article, string? warehouse, CancellationToken ct = default);

    /// <summary>Elimina una línea de la OC en Aurora.</summary>
    Task RemovePurchaseOrderArticleAsync(string externalId, string articleSku, string? warehouse, CancellationToken ct = default);
}
