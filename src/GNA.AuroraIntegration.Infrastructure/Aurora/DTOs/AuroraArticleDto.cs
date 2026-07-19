using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Application.DTOs.Aurora;

/// <summary>
/// Subobjeto reutilizado en Create/Update de artículo.
/// </summary>
public sealed class GroupOfArticleDto
{
    [JsonPropertyName("groupingCode")]
    public required string GroupingCode { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}

/// <summary>
/// POST /aurora-erp/articles
/// </summary>
public sealed class CreateAuroraArticleDto
{
    // Campos requeridos según doc (no marcados "// opcional")
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("ean")]
    public required string Ean { get; init; }

    [JsonPropertyName("eans")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Eans { get; init; }

    [JsonPropertyName("tagsIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? TagsIds { get; init; }

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

/// <summary>
/// PATCH /aurora-erp/articles/{sku}
/// </summary>
public sealed class UpdateAuroraArticleDto
{
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

    // ---- A partir de acá, campos inferidos por consistencia con el resto del doc ----

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

/// <summary>
/// GET /aurora-erp/articles/{sku}
/// </summary>
public sealed class AuroraArticleDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("bannerName")]
    public string? BannerName { get; set; }

    [JsonPropertyName("bannerExternalId")]
    public string? BannerExternalId { get; set; }

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("ean")]
    public string? Ean { get; set; }

    [JsonPropertyName("eans")]
    public List<string> Eans { get; set; } = new();

    [JsonPropertyName("weightInGr")]
    public decimal WeightInGr { get; set; }

    [JsonPropertyName("heightInCm")]
    public decimal HeightInCm { get; set; }

    [JsonPropertyName("widthInCm")]
    public decimal WidthInCm { get; set; }

    [JsonPropertyName("lengthInCm")]
    public decimal LengthInCm { get; set; }

    [JsonPropertyName("rotation")]
    public string? Rotation { get; set; }

    [JsonPropertyName("isConsumable")]
    public bool IsConsumable { get; set; }

    [JsonPropertyName("hasProductionBatch")]
    public bool HasProductionBatch { get; set; }

    [JsonPropertyName("hasDueDate")]
    public bool HasDueDate { get; set; }

    [JsonPropertyName("hasSerialNumber")]
    public bool HasSerialNumber { get; set; }

    [JsonPropertyName("colour")]
    public string? Colour { get; set; }

    [JsonPropertyName("bulky")]
    public bool Bulky { get; set; }

    [JsonPropertyName("cage")]
    public bool Cage { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }
}


public sealed class GroupOfArticleDto
{
    [JsonPropertyName("groupingCode")]
    public required string GroupingCode { get; init; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; init; }
}