using System.ComponentModel.DataAnnotations;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Client;

public sealed class ServiceLayerSettings
{
    [Required]
    [Url]
    public required string BaseUrl    { get; init; }

    [Required]
    [MinLength(1)]
    public required string CompanyDB  { get; init; }

    [Required]
    [MinLength(1)]
    public required string UserName   { get; init; }

    [Required]
    [MinLength(1)]
    public required string Password   { get; init; }
}
