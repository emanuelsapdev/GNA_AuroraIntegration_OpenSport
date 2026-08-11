using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Contrato específico de replicación para TransferOutOrder (ISP: el consumidor de Application
/// solo ve operaciones de TransferOutOrder, sin saber que por debajo hay una tabla compartida).
/// </summary>
public interface ITransferOutOrderReplicationRepository
{
    Task<IReadOnlyList<TransferOutOrder>> GetPendingTransferOutOrdersAsync(int batchSize = 100, CancellationToken ct = default);
    Task MarkTransferOutOrderAsReplicatedAsync(string docEntry, CancellationToken ct = default);
    Task MarkTransferOutOrderAsFailedAsync(string docEntry, string errorMessage, CancellationToken ct = default);
}
