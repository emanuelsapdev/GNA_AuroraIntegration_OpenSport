using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

/// <summary>
/// Respuesta de GET /aurora-erp/transfer-out-orders/{externalId}. Se usa para el chequeo
/// de idempotencia previo a la creación (evitar recrear una orden ya existente en Aurora).
/// Refleja el contrato documentado completo (incluye campos de envío/logística que esta
/// integración no consume hoy, pero que Aurora puede devolver).
/// </summary>
public sealed class AuroraTransferOutOrderDto
{
    [JsonPropertyName("externalId")]
    public string ExternalId { get; init; } = string.Empty;

    [JsonPropertyName("bannerName")]
    public string? BannerName { get; init; }

    [JsonPropertyName("bannerExternalId")]
    public string? BannerExternalId { get; init; }

    [JsonPropertyName("logisticOperatorName")]
    public string? LogisticOperatorName { get; init; }

    [JsonPropertyName("logisticOperatorExternalId")]
    public string? LogisticOperatorExternalId { get; init; }

    [JsonPropertyName("postalCode")]
    public string? PostalCode { get; init; }

    [JsonPropertyName("shippingPriorityName")]
    public string? ShippingPriorityName { get; init; }

    [JsonPropertyName("shippingPriorityExternalId")]
    public string? ShippingPriorityExternalId { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("articlesQuantity")]
    public int ArticlesQuantity { get; init; }

    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
