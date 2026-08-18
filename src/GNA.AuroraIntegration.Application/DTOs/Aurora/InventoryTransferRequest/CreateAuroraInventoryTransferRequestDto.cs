using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora.InventoryTransferRequest;

/// <summary>
/// Body de POST /aurora-erp/transfer-out-orders.
///
/// ⚠️ La documentación de Aurora NO marca "bannerExternalId", "logisticOperatorExternalId",
/// "postalCode" ni "shippingPriorityExternalId" como "// optional" (a diferencia de sus
/// contrapartes *Name, que sí lo están) — a diferencia de purchase-orders, donde
/// bannerExternalId/bannerName sí son ambos opcionales. Esta implementación, igual que
/// PurchaseOrderSyncUseCase con el banner, NO tiene hoy un campo SAP (OWTQ) mapeado a
/// ninguno de estos cuatro y los omite (quedan null → no se serializan). Riesgo real: si
/// Aurora los exige efectivamente para este recurso, el alta puede devolver 400. Pendiente
/// de definición de negocio antes de producción.
/// </summary>
public sealed class CreateAuroraInventoryTransferRequestDto
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

    // TODO (Etapa 1 - pendiente de definición de negocio): sin campo SAP mapeado. Ver
    // advertencia en el comentario de clase.
    [JsonPropertyName("logisticOperatorExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogisticOperatorExternalId { get; init; }

    [JsonPropertyName("postalCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PostalCode { get; init; }

    [JsonPropertyName("shippingPriorityExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShippingPriorityExternalId { get; init; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("articles")]
    public required InventoryTransferRequestArticleDto[] Articles { get; init; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; init; }
}
