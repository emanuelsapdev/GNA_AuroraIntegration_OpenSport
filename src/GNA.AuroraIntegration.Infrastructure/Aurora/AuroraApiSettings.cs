using System.ComponentModel.DataAnnotations;

namespace GNA.AuroraIntegration.Infrastructure.Aurora;

public sealed class AuroraApiSettings
{
    [Required]
    [Url]
    public required string BaseUrl { get; init; }

    [Required]
    [MinLength(1)]
    public required string ApiKey { get; init; }

    /// <summary>
    /// Identificador de depósito ("warehouse") requerido por los endpoints de Órdenes de
    /// Compra de Aurora (a diferencia de Artículos, donde es opcional). TODO: pendiente de
    /// definición de negocio — Etapa 1 asume un único depósito/CD, configurable acá.
    /// Si queda vacío, los requests a purchase-orders se envían sin el query param
    /// "warehouse" y Aurora puede rechazarlos.
    /// </summary>
    public string? Warehouse { get; init; }
}
