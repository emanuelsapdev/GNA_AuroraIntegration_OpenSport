using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

namespace GNA.AuroraIntegration.Infrastructure.Repositories;

/// <summary>
/// Adapta IInventoryTransferRequestReplicationRepository sobre el store genérico compartido,
/// resolviendo además la entidad InventoryTransferRequest completa a partir del DocEntry pendiente.
/// </summary>
public sealed class InventoryTransferRequestReplicationRepository : IInventoryTransferRequestReplicationRepository
{
    private readonly IReplicationControlStore _store;
    private readonly IInventoryTransferRequestLookupRepository _InventoryTransferRequestLookup;

    public InventoryTransferRequestReplicationRepository(
        IReplicationControlStore store, IInventoryTransferRequestLookupRepository InventoryTransferRequestLookup)
    {
        _store = store;
        _InventoryTransferRequestLookup = InventoryTransferRequestLookup;
    }

    public async Task<IReadOnlyList<InventoryTransferRequest>> GetPendingInventoryTransferRequestAsync(
        int batchSize = 100, CancellationToken ct = default)
    {
        var pendingKeys = await _store.GetPendingKeysAsync(
            ReplicableEntityType.InventoryTransferRequest, batchSize, ct);

        return await _InventoryTransferRequestLookup.GetByDocEntryListAsync(pendingKeys, ct);
    }

    public Task MarkInventoryTransferRequestAsReplicatedAsync(string docEntry, CancellationToken ct = default)
        => _store.MarkAsReplicatedAsync(
            ReplicableEntityType.InventoryTransferRequest, docEntry,
            SapB1ReplicationConstants.Queue.MaxRetryCounts.InventoryTransferRequest, ct);

    public Task MarkInventoryTransferRequestAsFailedAsync(string docEntry, string errorMessage, CancellationToken ct = default)
        => _store.MarkAsFailedAsync(
            ReplicableEntityType.InventoryTransferRequest, docEntry,
            SapB1ReplicationConstants.Queue.MaxRetryCounts.InventoryTransferRequest, errorMessage, ct);
}
