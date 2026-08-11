namespace GNA.AuroraIntegration.Domain.Entities;

/// <summary>
/// Línea de una Solicitud de Traslado (WTQ1 en SAP B1). Representa un artículo solicitado
/// con su cantidad, mapeado 1:1 al array "articles" del payload de creación en Aurora.
/// </summary>
public sealed class TransferOutOrderLine
{
    /// <summary>
    /// LineNum de WTQ1 en SAP B1. Se envía a Aurora como "lineOrder".
    /// ⚠️ No verificado con certeza el nombre exacto de campo expuesto por Service Layer para
    /// el recurso InventoryTransferRequests/DocumentLines — se asume análogo a POR1.LineNum
    /// por ser el patrón estándar de todo documento de marketing SAP B1. Verificar contra el
    /// ambiente antes de producción si surgen discrepancias.
    /// </summary>
    public required int LineOrder { get; init; }

    /// <summary>ItemCode de la línea (WTQ1.ItemCode). Debe existir previamente en Aurora
    /// (replicado por ArticleSyncUseCase) — esta línea no envía el objeto "article" completo.</summary>
    public required string ArticleSku { get; init; }

    /// <summary>Cantidad solicitada (WTQ1.Quantity). Se conserva como decimal en el dominio;
    /// la conversión a entero (formato esperado por Aurora) ocurre en el mapeo a DTO.</summary>
    public required decimal Quantity { get; init; }
}
