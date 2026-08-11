namespace GNA.AuroraIntegration.Domain.Entities;

/// <summary>
/// Línea de una Orden de Compra (POR1 en SAP B1). Representa un artículo solicitado
/// con su cantidad, mapeado 1:1 al array "articles" del payload de creación en Aurora.
/// </summary>
public sealed class PurchaseOrderLine
{
    /// <summary>LineNum de POR1 en SAP B1. Se envía a Aurora como "lineOrder".</summary>
    public required int LineOrder { get; init; }

    /// <summary>ItemCode de la línea (POR1.ItemCode). Debe existir previamente en Aurora
    /// (replicado por ArticleSyncUseCase) — esta línea no envía el objeto "article" completo.</summary>
    public required string ArticleSku { get; init; }

    /// <summary>Cantidad solicitada (POR1.Quantity). Se conserva como decimal en el dominio;
    /// la conversión a entero (formato esperado por Aurora) ocurre en el mapeo a DTO.</summary>
    public required decimal Quantity { get; init; }
}
