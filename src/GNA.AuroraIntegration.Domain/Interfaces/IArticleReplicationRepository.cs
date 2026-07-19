// Domain/Interfaces/IArticleReplicationRepository.cs (ajustado)
using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Contrato específico de replicación para Article (ISP: el consumidor de Application
/// solo ve operaciones de Article, sin saber que por debajo hay una tabla compartida).
/// </summary>
public interface IArticleReplicationRepository
{
    Task<IReadOnlyList<Article>> GetPendingArticlesAsync(int batchSize = 100, CancellationToken ct = default);
    Task MarkArticleAsReplicatedAsync(string sku, CancellationToken ct = default);
    Task MarkArticleAsFailedAsync(string sku, string errorMessage, CancellationToken ct = default);
}