namespace GNA.AuroraIntegration.Domain.Entities;

/// <summary>
/// Representa una Orden de Compra de SAP B1 (OPOR/POR1) replicada hacia Aurora WMS
/// como "purchase order". Entidad pura, sin dependencias del SDK de SAP ni de infraestructura.
/// </summary>
public sealed class PurchaseOrder
{
    /// <summary>
    /// DocEntry de la Orden de Compra en SAP B1. Es la clave natural utilizada como
    /// EntityKey en la cola de replicación y como externalId al crear la orden en Aurora
    /// (a diferencia de DocNum, DocEntry es inmutable y único independientemente de series/sucursales).
    /// </summary>
    public required int DocEntry { get; init; }

    /// <summary>Número de documento visible al usuario en SAP B1 (informativo, no se usa como clave).</summary>
    public int? DocNum { get; init; }

    /// <summary>
    /// true si la OC fue cancelada en SAP B1 (campo estándar OPOR.Cancelled, expuesto por
    /// Service Layer como "tYES"/"tNO"). PurchaseOrderSyncUseCase usa este flag —no el
    /// Operation con el que quedó encolada la entrada— para decidir si corresponde
    /// cancelar la OC en Aurora en lugar de crearla/reconciliarla.
    /// </summary>
    public bool Cancelled { get; init; }

    // TODO (Etapa 1 - pendiente de definición de negocio): Aurora admite bannerName/bannerExternalId
    // opcionales en la creación de la OC. No hay campo SAP mapeado hoy; se omiten en la replicación.
    public string? BannerName { get; init; }
    public string? BannerExternalId { get; init; }

    public string? Notes { get; init; }

    public required IReadOnlyList<PurchaseOrderLine> Lines { get; init; }
}
