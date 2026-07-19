using System.Net;
using System.Text.Json;
using RestSharp;
using RestSharp.Serializers.Json;
using GNA.AuroraIntegration.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Client;

/// <summary>
/// Cliente RestSharp hacia SAP B1 Service Layer. Maneja login y renovación de sesión
/// automáticamente (cookie B1SESSION). Sin estado de negocio.
/// </summary>
public sealed class ServiceLayerClient : IServiceLayerClient
{
    private readonly RestClient _client;
    private readonly ServiceLayerSettings _settings;
    private readonly ILogger<ServiceLayerClient> _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _authenticated;

    public ServiceLayerClient(
        IOptions<ServiceLayerSettings> settings,
        ILogger<ServiceLayerClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        RestClientOptions options = new(_settings.BaseUrl.TrimEnd('/') + "/")
        {
            // La cookie B1SESSION y ROUTEID persisten aquí durante toda la vida del Singleton
            CookieContainer = new CookieContainer(),
            // SAP B1 on-premise suele usar certificados auto-firmados
            RemoteCertificateValidationCallback = (_, _, _, _) => true,
        };

        // SAP B1 Service Layer requiere PascalCase en los campos JSON (CompanyDB, UserName, Password).
        // RestSharp 114 usa camelCase por defecto con System.Text.Json; se deshabilita esa política.
        _client = new RestClient(
            options,
            configureSerialization: s => s.UseSystemTextJson(
                new JsonSerializerOptions { PropertyNamingPolicy = null }));
        _client.AddDefaultHeader("Accept", "application/json");
        _client.AddDefaultHeader("Accept-Language", "es-ES");
    }

    public async Task<T?> GetAsync<T>(string resource, CancellationToken ct = default)
        => await SendAsync<T>(Method.Get, resource, null, ct);

    public async Task<T> PostAsync<T>(string resource, object body, CancellationToken ct = default)
        => (await SendAsync<T>(Method.Post, resource, body, ct))!;

    public async Task PatchAsync(string resource, object body, CancellationToken ct = default)
        => await SendAsync<object>(Method.Patch, resource, body, ct);

    public async Task DeleteAsync(string resource, CancellationToken ct = default)
        => await SendAsync<object>(Method.Delete, resource, null, ct);

    private async Task<T?> SendAsync<T>(
        Method method, string resource, object? body, CancellationToken ct, bool isRetry = false)
    {
        await EnsureAuthenticatedAsync(ct);

        RestRequest request = new(resource.TrimStart('/'), method);
        if (body is not null)
            request.AddJsonBody(body);

        RestResponse<T> response = await _client.ExecuteAsync<T>(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _authenticated = false;
            return await SendAsync<T>(method, resource, body, ct, isRetry: true);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        if (!response.IsSuccessful)
        {
            throw new HttpRequestException(
                $"Service Layer error {(int)response.StatusCode} en {method} {resource}: {response.Content}");
        }

        return response.Data;
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (_authenticated) return;

        await _loginLock.WaitAsync(ct);
        try
        {
            if (_authenticated) return;

            _logger.LogInformation("Autenticando contra Service Layer: {BaseUrl}", _settings.BaseUrl);

            var loginBody = new
            {
                CompanyDB = _settings.CompanyDB,
                UserName  = _settings.UserName,
                Password  = _settings.Password
            };

            RestRequest request = new("Login", Method.Post);
            request.AddJsonBody(loginBody);

            _logger.LogDebug("Enviando POST /Login a {BaseUrl} para usuario {UserName}",
                _settings.BaseUrl, _settings.UserName);

            RestResponse response = await _client.ExecuteAsync(request, ct);

            if (!response.IsSuccessful)
            {
                _logger.LogError("Login fallido con status {StatusCode}. Response: {Error}",
                    response.StatusCode, response.Content);
                throw new HttpRequestException(
                    $"Service Layer login fallido con status {(int)response.StatusCode}: {response.Content}");
            }

            // La cookie B1SESSION y ROUTEID quedan almacenadas en el CookieContainer
            // del RestClientOptions configurado en el constructor.
            _authenticated = true;
            _logger.LogInformation("Service Layer autenticado exitosamente para {CompanyDB}", _settings.CompanyDB);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            _logger.LogError(ex, "Excepción durante autenticación Service Layer");
            throw;
        }
        finally
        {
            _loginLock.Release();
        }
    }
}

