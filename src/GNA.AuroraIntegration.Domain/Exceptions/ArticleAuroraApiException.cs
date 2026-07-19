namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// Error al replicar un artículo hacia Aurora (falla de alta/modificación vía API).
/// </summary>
public sealed class ArticleAuroraApiException : AuroraIntegrationException
{
    private readonly string sku;

    public ArticleAuroraApiException(string sku,string message)
        : base(message)
    {
        this.sku = sku;
    }

    public ArticleAuroraApiException(string sku, string message, Exception inner)
        : base(message, inner)
    {
        this.sku = sku;
    }
}