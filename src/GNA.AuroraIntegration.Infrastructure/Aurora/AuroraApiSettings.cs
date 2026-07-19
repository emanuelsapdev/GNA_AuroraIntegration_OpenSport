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
}