using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora.PurchaseOrder;

/// <summary>
/// Body de POST /aurora-erp/purchase-orders.
/// </summary>
public sealed class CreateAuroraPurchaseOrderDto
{
    [Required]
    [JsonPropertyName("externalId")]
    public required string ExternalId { get; init; }

    [JsonPropertyName("bannerName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BannerName { get; init; }

    [JsonPropertyName("bannerExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BannerExternalId { get; init; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("articles")]
    public required PurchaseOrderArticleDto[] Articles { get; init; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; init; }
}
