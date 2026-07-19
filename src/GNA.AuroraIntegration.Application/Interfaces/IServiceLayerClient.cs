namespace GNA.AuroraIntegration.Application.Interfaces;

/// <summary>
/// Abstracción del cliente HTTP hacia SAP B1 Service Layer.
/// Gestiona sesión/autenticación de forma transparente para el consumidor.
/// </summary>
public interface IServiceLayerClient
{
    Task<T?> GetAsync<T>(string resource, CancellationToken ct = default);
    Task<T> PostAsync<T>(string resource, object body, CancellationToken ct = default);
    Task PatchAsync(string resource, object body, CancellationToken ct = default);
    Task DeleteAsync(string resource, CancellationToken ct = default);
}