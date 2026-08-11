using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Contrato específico de replicación para PurchaseOrder (ISP: el consumidor de Application
/// solo ve operaciones de PurchaseOrder, sin saber que por debajo hay una tabla compartida).
/// </summary>
public interface IPurchaseOrderReplicationRepository
{
    Task<IReadOnlyList<PurchaseOrder>> GetPendingPurchaseOrdersAsync(int batchSize = 100, CancellationToken ct = default);
    Task MarkPurchaseOrderAsReplicatedAsync(string docEntry, CancellationToken ct = default);
    Task MarkPurchaseOrderAsFailedAsync(string docEntry, string errorMessage, CancellationToken ct = default);
}
