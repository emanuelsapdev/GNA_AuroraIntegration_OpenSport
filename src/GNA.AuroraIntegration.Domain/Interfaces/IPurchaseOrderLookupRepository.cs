using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Acceso de solo lectura a los datos de PurchaseOrder como entidad de negocio en SAP B1
/// (fuente de verdad antes de replicar hacia Aurora). No gestiona estado de
/// replicación — para eso existe IPurchaseOrderReplicationRepository.
/// </summary>
public interface IPurchaseOrderLookupRepository
{
    /// <summary>
    /// Resuelve una Orden de Compra por DocEntry. Devuelve null si no existe (nunca lanza
    /// PurchaseOrderNotFoundException acá: la decisión de qué hacer ante un DocEntry
    /// inexistente le corresponde al consumidor, ej. marcarlo como Discarded).
    /// </summary>
    Task<PurchaseOrder?> GetByDocEntryAsync(string docEntry, CancellationToken ct = default);

    /// <summary>
    /// Resuelve varias Órdenes de Compra en una sola consulta, para no hacer N round-trips
    /// al procesar un batch de DocEntry pendientes de replicación.
    /// </summary>
    Task<IReadOnlyList<PurchaseOrder>> GetByDocEntryListAsync(
        IEnumerable<string> docEntries, CancellationToken ct = default);
}
