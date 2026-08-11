using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

namespace GNA.AuroraIntegration.Infrastructure.Repositories;

/// <summary>
/// Adapta ITransferOutOrderReplicationRepository sobre el store genérico compartido,
/// resolviendo además la entidad TransferOutOrder completa a partir del DocEntry pendiente.
/// </summary>
public sealed class TransferOutOrderReplicationRepository : ITransferOutOrderReplicationRepository
{
    private readonly IReplicationControlStore _store;
    private readonly IInventoryTransferRequestLookupRepository _transferOutOrderLookup;

    public TransferOutOrderReplicationRepository(
        IReplicationControlStore store, IInventoryTransferRequestLookupRepository transferOutOrderLookup)
    {
        _store = store;
        _transferOutOrderLookup = transferOutOrderLookup;
    }

    public async Task<IReadOnlyList<TransferOutOrder>> GetPendingTransferOutOrdersAsync(
        int batchSize = 100, CancellationToken ct = default)
    {
        var pendingKeys = await _store.GetPendingKeysAsync(
            ReplicableEntityType.TransferOutOrder, batchSize, ct);

        return await _transferOutOrderLookup.GetByDocEntryListAsync(pendingKeys, ct);
    }

    public Task MarkTransferOutOrderAsReplicatedAsync(string docEntry, CancellationToken ct = default)
        => _store.MarkAsReplicatedAsync(
            ReplicableEntityType.TransferOutOrder, docEntry,
            SapB1ReplicationConstants.Queue.MaxRetryCounts.TransferOutOrder,
            SapB1ReplicationConstants.Queue.ExcludedStatuses.TransferOutOrder, ct);

    public Task MarkTransferOutOrderAsFailedAsync(string docEntry, string errorMessage, CancellationToken ct = default)
        => _store.MarkAsFailedAsync(
            ReplicableEntityType.TransferOutOrder, docEntry,
            SapB1ReplicationConstants.Queue.MaxRetryCounts.TransferOutOrder, errorMessage,
            SapB1ReplicationConstants.Queue.ExcludedStatuses.TransferOutOrder, ct);
}
