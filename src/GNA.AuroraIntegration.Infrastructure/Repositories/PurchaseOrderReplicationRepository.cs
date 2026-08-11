using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

namespace GNA.AuroraIntegration.Infrastructure.Repositories;

/// <summary>
/// Adapta IPurchaseOrderReplicationRepository sobre el store genérico compartido,
/// resolviendo además la entidad PurchaseOrder completa a partir del DocEntry pendiente.
/// </summary>
public sealed class PurchaseOrderReplicationRepository : IPurchaseOrderReplicationRepository
{
    private readonly IReplicationControlStore _store;
    private readonly IPurchaseOrderLookupRepository _purchaseOrderLookup;

    public PurchaseOrderReplicationRepository(
        IReplicationControlStore store, IPurchaseOrderLookupRepository purchaseOrderLookup)
    {
        _store = store;
        _purchaseOrderLookup = purchaseOrderLookup;
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetPendingPurchaseOrdersAsync(
        int batchSize = 100, CancellationToken ct = default)
    {
        var pendingKeys = await _store.GetPendingKeysAsync(
            ReplicableEntityType.PurchaseOrder, batchSize, ct);

        return await _purchaseOrderLookup.GetByDocEntryListAsync(pendingKeys, ct);
    }

    public Task MarkPurchaseOrderAsReplicatedAsync(string docEntry, CancellationToken ct = default)
        => _store.MarkAsReplicatedAsync(
            ReplicableEntityType.PurchaseOrder, docEntry,
            SapB1ReplicationConstants.Queue.MaxRetryCounts.PurchaseOrder,
            SapB1ReplicationConstants.Queue.ExcludedStatuses.PurchaseOrder, ct);

    public Task MarkPurchaseOrderAsFailedAsync(string docEntry, string errorMessage, CancellationToken ct = default)
        => _store.MarkAsFailedAsync(
            ReplicableEntityType.PurchaseOrder, docEntry,
            SapB1ReplicationConstants.Queue.MaxRetryCounts.PurchaseOrder, errorMessage,
            SapB1ReplicationConstants.Queue.ExcludedStatuses.PurchaseOrder, ct);
}
