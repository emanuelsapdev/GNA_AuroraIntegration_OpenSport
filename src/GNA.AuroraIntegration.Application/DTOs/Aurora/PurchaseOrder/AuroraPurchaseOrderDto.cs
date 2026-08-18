using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora.PurchaseOrder;

/// <summary>
/// Respuesta de GET /aurora-erp/purchase-orders/{externalId}. Se usa para el chequeo
/// de idempotencia previo a la creación (evitar recrear una OC ya existente en Aurora).
/// </summary>
public sealed class AuroraPurchaseOrderDto
{
    [JsonPropertyName("externalId")]
    public string ExternalId { get; init; } = string.Empty;

    [JsonPropertyName("bannerName")]
    public string? BannerName { get; init; }

    [JsonPropertyName("bannerExternalId")]
    public string? BannerExternalId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("articlesQuantity")]
    public int ArticlesQuantity { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
