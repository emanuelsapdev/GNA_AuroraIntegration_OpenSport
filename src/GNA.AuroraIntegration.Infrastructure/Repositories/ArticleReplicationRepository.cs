using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

namespace GNA.AuroraIntegration.Infrastructure.Repositories;

/// <summary>
/// Adapta IArticleReplicationRepository sobre el store genérico compartido,
/// resolviendo además la entidad Article completa a partir del SKU pendiente.
/// </summary>
public sealed class ArticleReplicationRepository : IArticleReplicationRepository
{
    private readonly IReplicationControlStore _store;
    private readonly IArticleLookupRepository _articleLookup; // trae el Article completo por SKU

    public ArticleReplicationRepository(
        IReplicationControlStore store, IArticleLookupRepository articleLookup)
    {
        _store = store;
        _articleLookup = articleLookup;
    }

    public async Task<IReadOnlyList<Article>> GetPendingArticlesAsync(
        int batchSize = 100, CancellationToken ct = default)
    {
        var pendingSkus = await _store.GetPendingKeysAsync(
        ReplicableEntityType.Article, batchSize, ct);

        return await _articleLookup.GetBySkuListAsync(pendingSkus, ct);
    }

    public Task MarkArticleAsReplicatedAsync(string sku, CancellationToken ct = default)
        => _store.MarkAsReplicatedAsync(ReplicableEntityType.Article, sku, SapB1ReplicationConstants.Queue.MaxRetryCounts.Article, SapB1ReplicationConstants.Queue.ExcludedStatuses.Article, ct);

    public Task MarkArticleAsFailedAsync(string sku, string errorMessage, CancellationToken ct = default)
        => _store.MarkAsFailedAsync(ReplicableEntityType.Article, sku, SapB1ReplicationConstants.Queue.MaxRetryCounts.Article, errorMessage, SapB1ReplicationConstants.Queue.ExcludedStatuses.Article, ct);
}