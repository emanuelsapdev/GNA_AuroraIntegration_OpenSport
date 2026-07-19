using GNA.AuroraIntegration.Domain.Entities;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Acceso de solo lectura a los datos de Article como entidad de negocio en SAP B1
/// (fuente de verdad antes de replicar hacia Aurora). No gestiona estado de
/// replicación — para eso existe IArticleReplicationRepository.
/// </summary>
public interface IArticleLookupRepository
{
    /// <summary>
    /// Resuelve un artículo por SKU. Devuelve null si no existe (nunca lanza
    /// ArticleNotFoundException acá: la decisión de qué hacer ante un SKU
    /// inexistente le corresponde al consumidor, ej. marcarlo como Discarded).
    /// </summary>
    Task<Article?> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// Resuelve varios artículos en una sola consulta, para no hacer N round-trips
    /// al procesar un batch de SKUs pendientes de replicación.
    /// </summary>
    Task<IReadOnlyList<Article>> GetBySkuListAsync(
        IEnumerable<string> skus, CancellationToken ct = default);
}
