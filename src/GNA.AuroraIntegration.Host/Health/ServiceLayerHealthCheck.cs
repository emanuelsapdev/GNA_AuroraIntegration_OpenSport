using GNA.AuroraIntegration.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GNA.AuroraIntegration.Host.Health;

public sealed class ServiceLayerHealthCheck : IHealthCheck
{
    private readonly IServiceLayerClient _serviceLayerClient;

    public ServiceLayerHealthCheck(IServiceLayerClient serviceLayerClient)
    {
        _serviceLayerClient = serviceLayerClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _serviceLayerClient.GetAsync<object>("Users?$top=1", cancellationToken);
            return HealthCheckResult.Healthy("Conectividad con Service Layer verificada.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("No fue posible conectarse con Service Layer.", ex);
        }
    }
}
