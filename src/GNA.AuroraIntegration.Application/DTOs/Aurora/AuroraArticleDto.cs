using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

public sealed class AuroraArticleDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("bannerName")]
    public string? BannerName { get; init; }

    [JsonPropertyName("bannerExternalId")]
    public string? BannerExternalId { get; init; }

    [JsonPropertyName("sku")]
    public string Sku { get; init; } = string.Empty;

    [JsonPropertyName("ean")]
    public string? Ean { get; init; }

    [JsonPropertyName("eans")]
    public IReadOnlyList<string> Eans { get; init; } = [];

    [JsonPropertyName("weightInGr")]
    public decimal WeightInGr { get; init; }

    [JsonPropertyName("heightInCm")]
    public decimal HeightInCm { get; init; }

    [JsonPropertyName("widthInCm")]
    public decimal WidthInCm { get; init; }

    [JsonPropertyName("lengthInCm")]
    public decimal LengthInCm { get; init; }

    [JsonPropertyName("rotation")]
    public string? Rotation { get; init; }

    [JsonPropertyName("isConsumable")]
    public bool IsConsumable { get; init; }

    [JsonPropertyName("hasProductionBatch")]
    public bool HasProductionBatch { get; init; }

    [JsonPropertyName("hasDueDate")]
    public bool HasDueDate { get; init; }

    [JsonPropertyName("hasSerialNumber")]
    public bool HasSerialNumber { get; init; }

    [JsonPropertyName("colour")]
    public string? Colour { get; init; }

    [JsonPropertyName("bulky")]
    public bool Bulky { get; init; }

    [JsonPropertyName("cage")]
    public bool Cage { get; init; }

    [JsonPropertyName("size")]
    public string? Size { get; init; }
}
