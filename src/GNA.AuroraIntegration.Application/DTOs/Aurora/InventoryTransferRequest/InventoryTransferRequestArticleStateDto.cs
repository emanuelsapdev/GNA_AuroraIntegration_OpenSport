using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora.InventoryTransferRequest;

/// <summary>
/// Item de la respuesta de GET /aurora-erp/transfer-out-orders/{externalId}/articles.
/// Incluye la cantidad ya cumplida (fulfilledQuantity) en el depósito, clave para decidir
/// si una línea puede editarse/eliminarse de forma segura durante la reconciliación.
/// </summary>
public sealed class InventoryTransferRequestArticleStateDto
{
    [JsonPropertyName("articleName")]
    public string? ArticleName { get; init; }

    [JsonPropertyName("articleSku")]
    public string ArticleSku { get; init; } = string.Empty;

    [JsonPropertyName("articleEan")]
    public string? ArticleEan { get; init; }

    [JsonPropertyName("requestedQuantity")]
    public int RequestedQuantity { get; init; }

    [JsonPropertyName("fulfilledQuantity")]
    public int FulfilledQuantity { get; init; }
}
