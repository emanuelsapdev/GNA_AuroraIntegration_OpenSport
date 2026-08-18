using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Contrato específico de replicación para InventoryTransferRequest (ISP: el consumidor de Application
/// solo ve operaciones de InventoryTransferRequest, sin saber que por debajo hay una tabla compartida).
/// </summary>
public interface IInventoryTransferRequestReplicationRepository
{
    Task<IReadOnlyList<InventoryTransferRequest>> GetPendingInventoryTransferRequestAsync(int batchSize = 100, CancellationToken ct = default);
    Task MarkInventoryTransferRequestAsReplicatedAsync(string docEntry, CancellationToken ct = default);
    Task MarkInventoryTransferRequestAsFailedAsync(string docEntry, string errorMessage, CancellationToken ct = default);
}
