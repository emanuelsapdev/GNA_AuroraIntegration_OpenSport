using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

public sealed class GroupOfArticleDto
{
    [JsonPropertyName("groupingCode")]
    public required string GroupingCode { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}
