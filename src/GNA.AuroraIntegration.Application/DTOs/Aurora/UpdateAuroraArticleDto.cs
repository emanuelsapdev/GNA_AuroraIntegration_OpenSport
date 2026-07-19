using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

public sealed class UpdateAuroraArticleDto
{
    [Required]
    [JsonPropertyName("sku")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sku { get; init; }

    [JsonPropertyName("eans")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Eans { get; init; }

    [JsonPropertyName("tagsIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? TagsIds { get; init; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("bannerName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BannerName { get; init; }

    [JsonPropertyName("bannerExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BannerExternalId { get; init; }

    [JsonPropertyName("brandName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BrandName { get; init; }

    [JsonPropertyName("brandExternalId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BrandExternalId { get; init; }

    [JsonPropertyName("categoryName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CategoryName { get; init; }

    [JsonPropertyName("weightInGr")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? WeightInGr { get; init; }

    [JsonPropertyName("heightInCm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? HeightInCm { get; init; }

    [JsonPropertyName("widthInCm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? WidthInCm { get; init; }

    [JsonPropertyName("lengthInCm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LengthInCm { get; init; }

    [JsonPropertyName("isConsumable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsConsumable { get; init; }

    [JsonPropertyName("hasProductionBatch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasProductionBatch { get; init; }

    [JsonPropertyName("hasDueDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasDueDate { get; init; }

    [JsonPropertyName("hasSerialNumber")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasSerialNumber { get; init; }

    [JsonPropertyName("colour")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Colour { get; init; }

    [JsonPropertyName("isUsedForPackaging")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsUsedForPackaging { get; init; }

    [JsonPropertyName("hasProductIdentifier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasProductIdentifier { get; init; }

    [JsonPropertyName("safetyStock")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SafetyStock { get; init; }

    [JsonPropertyName("useSafetyStock")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? UseSafetyStock { get; init; }

    [JsonPropertyName("bulky")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Bulky { get; init; }

    [JsonPropertyName("cage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Cage { get; init; }

    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Size { get; init; }

    [JsonPropertyName("groupsOfArticle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroupOfArticleDto[]? GroupsOfArticle { get; init; }
}
