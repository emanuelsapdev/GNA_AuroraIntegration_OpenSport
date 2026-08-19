namespace GNA.AuroraIntegration.Domain.Entities;

/// <summary>
/// Representa una Solicitud de Traslado de SAP B1 (OWTQ/WTQ1, "Inventory Transfer Request")
/// replicada hacia Aurora WMS como "transfer out order" (envíos a Sucursales). Entidad pura,
/// sin dependencias del SDK de SAP ni de infraestructura.
/// </summary>
public sealed class InventoryTransferRequest
{
    /// <summary>
    /// DocEntry de la Solicitud de Traslado en SAP B1. Es la clave natural utilizada como
    /// EntityKey en la cola de replicación y como externalId al crear la orden en Aurora
    /// (a diferencia de DocNum, DocEntry es inmutable y único independientemente de series/sucursales).
    /// </summary>
    public required int DocEntry { get; init; }

    /// <summary>Número de documento visible al usuario en SAP B1 (informativo, no se usa como clave).</summary>
    public int? DocNum { get; init; }

    public bool IsClosedManual { get; init; }

    // TODO (Etapa 1 - pendiente de definición de negocio): la documentación de Aurora no marca
    // bannerExternalId/logisticOperatorExternalId/postalCode/shippingPriorityExternalId como
    // opcionales en la creación de la orden (a diferencia de purchase-orders, donde bannerName/
    // bannerExternalId sí lo son). No hay campo SAP mapeado hoy a ninguno de estos cuatro; se
    // omiten en la replicación. ⚠️ Riesgo: si Aurora los exige realmente, el alta puede fallar
    // con 400 — validar contra el ambiente de Aurora antes de pasar a producción.
    public string? BannerName { get; init; }
    public string? BannerExternalId { get; init; }

    public string? Notes { get; init; }

    public required IReadOnlyList<InventoryTransferRequestLine> Lines { get; init; }
    /// <summary>Code de la fila en @GNA_AUR_REP_QUEUE que originó esta orden pendiente.</summary>
    public string? QueueCode { get; init; }
}
