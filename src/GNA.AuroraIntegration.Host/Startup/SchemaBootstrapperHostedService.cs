// Host/Startup/SchemaBootstrapperHostedService.cs
using GNA.AuroraIntegration.Application.UseCases;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Host.Startup;

/// <summary>
/// Corre una única vez al arrancar el Host, antes de que Quartz dispare jobs,
/// para garantizar que el esquema de replicación exista en SAP B1.
/// </summary>
public sealed class SchemaBootstrapperHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SchemaBootstrapperHostedService> _logger;

    public SchemaBootstrapperHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SchemaBootstrapperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Verificando esquema de replicación en SAP B1...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IEnsureReplicationSchemaUseCase>();
            await useCase.ExecuteAsync(ct);
            _logger.LogInformation("Esquema de replicación verificado correctamente.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fallo al provisionar el esquema de replicación. El servicio no puede continuar.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}