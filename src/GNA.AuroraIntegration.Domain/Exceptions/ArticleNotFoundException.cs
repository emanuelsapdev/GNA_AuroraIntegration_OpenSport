namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// El artículo esperado no fue encontrado en la fuente de datos correspondiente.
/// </summary>
public sealed class ArticleNotFoundException : AuroraIntegrationException
{
    public string Sku { get; }

    public ArticleNotFoundException(string sku)
        : base($"No se encontró el artículo con SKU '{sku}'.")
    {
        Sku = sku;
    }
}