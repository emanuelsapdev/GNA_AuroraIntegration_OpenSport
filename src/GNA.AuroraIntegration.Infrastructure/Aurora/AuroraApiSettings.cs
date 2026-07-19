using System.ComponentModel.DataAnnotations;

namespace GNA.AuroraIntegration.Infrastructure.Aurora;

public sealed class AuroraApiSettings
{
    [Required]
    public required string BaseUrl { get; init; }

    [Required]
    public required string ApiKey { get; init; }
}