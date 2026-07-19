using GNA.AuroraIntegration.Domain.Enums;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Acceso genérico a la tabla física compartida de control de replicación.
/// No se expone a Application: es un detalle de implementación que
/// las interfaces específicas por entidad (IArticleReplicationRepository, etc.)
/// usan internamente, filtrando por ReplicableEntityType.
/// </summary>
public interface IReplicationControlStore
{
    Task<IReadOnlyList<string>> GetPendingKeysAsync(
        ReplicableEntityType entityType, int batchSize, CancellationToken ct = default);

    Task EnqueueAsync(
        ReplicableEntityType entityType, string entityKey,
        ReplicationOperationType operationType, CancellationToken ct = default);

    Task MarkAsReplicatedAsync(
        ReplicableEntityType entityType, string entityKey, CancellationToken ct = default);

    Task MarkAsFailedAsync(
        ReplicableEntityType entityType, string entityKey, string errorMessage, CancellationToken ct = default);
}