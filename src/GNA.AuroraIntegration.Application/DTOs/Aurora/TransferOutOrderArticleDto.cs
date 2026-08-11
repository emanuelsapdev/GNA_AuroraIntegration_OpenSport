using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

/// <summary>
/// Línea de artículo dentro del payload de creación/edición de una orden de transferencia
/// externa en Aurora. Solo se envían lineOrder/articleSku/quantity: se asume que el artículo
/// ya fue replicado previamente por ArticleSyncUseCase, por lo que no se incluye el objeto
/// "article" (alta de artículo nuevo embebido) que la API de Aurora admite como opcional —
/// mismo criterio ya aplicado en PurchaseOrderArticleDto.
/// </summary>
public sealed class TransferOutOrderArticleDto
{
    [JsonPropertyName("lineOrder")]
    public required int LineOrder { get; init; }

    [JsonPropertyName("articleSku")]
    public required string ArticleSku { get; init; }

    [JsonPropertyName("quantity")]
    public required int Quantity { get; init; }
}
