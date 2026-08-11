using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

/// <summary>
/// Body de PATCH /aurora-erp/transfer-out-orders/{externalId} — modificación puntual de
/// cabecera. A diferencia de purchase-orders (que no tiene PATCH de cabecera),
/// transfer-out-orders sí lo expone, pero Aurora documenta una precondición de estado:
/// "Estado de la orden -> PENDIENTE, CONGELADA. Utilizar estado TO_EDIT para modificar
/// el pedido."
///
/// ⚠️ Este DTO existe para exponer la capacidad en IAuroraTransferOutOrderApiClient, pero
/// TransferOutOrderSyncUseCase NO lo invoca automáticamente: la transición de estado a
/// TO_EDIT (POST .../transitions) no está implementada en esta etapa por no existir
/// precedente ni definición de negocio sobre cuándo dispararla — inventar ese
/// comportamiento violaría el principio de "no inventar" del proyecto. Queda como
/// limitación conocida / backlog (ver PROJECT_PROGRESS.md).
/// </summary>
public sealed class UpdateAuroraTransferOutOrderDto
{
    [JsonPropertyName("bannerName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BannerName { get; init; }

    [JsonPropertyName("bannerExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BannerExternalId { get; init; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; init; }
}
