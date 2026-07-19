namespace GNA.AuroraIntegration.Domain.Entities;

/// <summary>
/// Representa un artículo replicado entre SAP B1 y Aurora WMS.
/// Entidad pura, sin dependencias del SDK de SAP ni de infraestructura.
/// </summary>
public sealed class Article
{
    public required string Sku { get; init; }
    public required string Name { get; init; }

    public required string PrimaryEan { get; init; }
    public IReadOnlyList<string> AdditionalEans { get; init; } = [];

    public decimal WeightInGr { get; init; }
    public decimal HeightInCm { get; init; }
    public decimal WidthInCm { get; init; }
    public decimal LengthInCm { get; init; }

    public string? Colour { get; init; }
    public string? Size { get; init; }
    public bool HasProductionBatch { get; init; }
    public bool HasDueDate { get; init; }
    public bool HasSerialNumber { get; init; }
    public bool IsConsumable { get; init; }

    public string? BrandName { get; init; }
    public string? CategoryName { get; init; }
}

