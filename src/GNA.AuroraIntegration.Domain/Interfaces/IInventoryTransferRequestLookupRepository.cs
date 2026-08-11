using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Acceso de solo lectura a los datos de TransferOutOrder (Solicitud de Traslado, OWTQ/WTQ1)
/// como entidad de negocio en SAP B1 (fuente de verdad antes de replicar hacia Aurora). No
/// gestiona estado de replicación — para eso existe ITransferOutOrderReplicationRepository.
/// </summary>
public interface IInventoryTransferRequestLookupRepository
{
    /// <summary>
    /// Resuelve una Solicitud de Traslado por DocEntry. Devuelve null si no existe (nunca
    /// lanza TransferOutOrderNotFoundException acá: la decisión de qué hacer ante un DocEntry
    /// inexistente le corresponde al consumidor, ej. marcarlo como Discarded).
    /// </summary>
    Task<TransferOutOrder?> GetByDocEntryAsync(string docEntry, CancellationToken ct = default);

    /// <summary>
    /// Resuelve varias Solicitudes de Traslado en una sola consulta, para no hacer N
    /// round-trips al procesar un batch de DocEntry pendientes de replicación.
    /// </summary>
    Task<IReadOnlyList<TransferOutOrder>> GetByDocEntryListAsync(
        IEnumerable<string> docEntries, CancellationToken ct = default);
}
