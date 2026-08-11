using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using RestSharp;
using System.Net;
using System.Text.Json;

namespace GNA.AuroraIntegration.Infrastructure.Aurora;

/// <summary>
/// Cliente HTTP hacia los endpoints de Órdenes de Compra ("purchase-orders") de Aurora WMS.
/// Mismo patrón de resiliencia (retry + circuit breaker) que AuroraArticleApiClient.
/// </summary>
public sealed class AuroraPurchaseOrderApiClient : IAuroraPurchaseOrderApiClient
{
    private const string Endpoint = "aurora-erp/purchase-orders";

    private readonly RestClient _client;
    private readonly AsyncPolicy _resiliencePolicy;
    private readonly ILogger<AuroraPurchaseOrderApiClient> _logger;
    private readonly string? _defaultWarehouse;

    public AuroraPurchaseOrderApiClient(IOptions<AuroraApiSettings> settings, ILogger<AuroraPurchaseOrderApiClient> logger)
    {
        _logger = logger;

        AuroraApiSettings cfg = settings.Value;
        _defaultWarehouse = cfg.Warehouse;

        if (string.IsNullOrWhiteSpace(_defaultWarehouse))
        {
            // No es fatal: se documenta como TODO en AuroraApiSettings. Se loguea acá porque
            // Aurora exige "warehouse" en estos endpoints (a diferencia de Artículos) y sin él
            // es probable que las creaciones de OC sean rechazadas.
            _logger.LogWarning(
                "AuroraApi:Warehouse no está configurado. Los requests a '{Endpoint}' se enviarán sin el query param 'warehouse'.",
                Endpoint);
        }

        RestClientOptions options = new(cfg.BaseUrl.TrimEnd('/') + "/");

        _client = new RestClient(options);
        _client.AddDefaultHeader("X-Api-Key", cfg.ApiKey);
        _client.AddDefaultHeader("Accept", "application/json");

        AsyncPolicy retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        AsyncPolicy circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

        _resiliencePolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
    }

    public async Task CreatePurchaseOrderAsync(CreateAuroraPurchaseOrderDto purchaseOrder, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new(Endpoint, Method.Post);

        AddWarehouseParameter(request, warehouse);

        var json = JsonSerializer.Serialize(purchaseOrder);
        request.AddJsonBody(json);

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Post, Endpoint);
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte creando Orden de Compra '{ExternalId}' en Aurora.", purchaseOrder.ExternalId);

            throw new PurchaseOrderAuroraApiException(
                purchaseOrder.ExternalId, $"Error de conexión al crear la Orden de Compra '{purchaseOrder.ExternalId}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} creando Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, purchaseOrder.ExternalId, response.Content);

            throw new PurchaseOrderAuroraApiException(
                purchaseOrder.ExternalId, $"Error creando Orden de Compra {purchaseOrder.ExternalId}: {response.StatusCode}");
        }
    }

    public async Task<AuroraPurchaseOrderDto?> GetPurchaseOrderByExternalIdAsync(string externalId, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{externalId}", Method.Get);
        AddWarehouseParameter(request, warehouse);

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Get, $"{Endpoint}/{externalId}");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte obteniendo Orden de Compra '{ExternalId}' en Aurora.", externalId);
            throw new PurchaseOrderAuroraApiException(
                externalId, $"Error de conexión al obtener la Orden de Compra '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} obteniendo Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);
            throw new PurchaseOrderAuroraApiException(externalId, $"Error obteniendo Orden de Compra {externalId}: {response.StatusCode}");
        }

        return JsonSerializer.Deserialize<AuroraPurchaseOrderDto?>(response.Content ?? string.Empty);
    }

    public async Task<IReadOnlyList<PurchaseOrderArticleStateDto>> GetPurchaseOrderArticlesAsync(string externalId, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{externalId}/articles", Method.Get);
        AddWarehouseParameter(request, warehouse);

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Get, $"{Endpoint}/{externalId}/articles");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte obteniendo líneas de la Orden de Compra '{ExternalId}' en Aurora.", externalId);
            throw new PurchaseOrderAuroraApiException(
                externalId, $"Error de conexión al obtener líneas de la Orden de Compra '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} obteniendo líneas de la Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);
            throw new PurchaseOrderAuroraApiException(externalId, $"Error obteniendo líneas de la Orden de Compra {externalId}: {response.StatusCode}");
        }

        return JsonSerializer.Deserialize<List<PurchaseOrderArticleStateDto>>(response.Content ?? "[]") ?? [];
    }

    public async Task AddPurchaseOrderArticlesAsync(string externalId, IReadOnlyList<PurchaseOrderArticleDto> articles, string? warehouse, CancellationToken ct = default)
    {
        // Guarda defensiva: el use case ya evita llamar acá con lista vacía, pero el cliente
        // no debe asumirlo — un POST con array vacío no tiene sentido de negocio.
        if (articles.Count == 0)
        {
            return;
        }

        RestRequest request = new($"{Endpoint}/{externalId}/articles", Method.Post);
        AddWarehouseParameter(request, warehouse);
        request.AddJsonBody(JsonSerializer.Serialize(articles));

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Post, $"{Endpoint}/{externalId}/articles");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte agregando líneas a la Orden de Compra '{ExternalId}' en Aurora.", externalId);
            throw new PurchaseOrderAuroraApiException(
                externalId, $"Error de conexión al agregar líneas a la Orden de Compra '{externalId}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} agregando líneas a la Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);
            throw new PurchaseOrderAuroraApiException(externalId, $"Error agregando líneas a la Orden de Compra {externalId}: {response.StatusCode}");
        }
    }

    public async Task UpdatePurchaseOrderArticleAsync(string externalId, string articleSku, PurchaseOrderArticleDto article, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{externalId}/articles/{articleSku}", Method.Patch);
        AddWarehouseParameter(request, warehouse);
        request.AddJsonBody(JsonSerializer.Serialize(article));

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Patch, $"{Endpoint}/{externalId}/articles/{articleSku}");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte editando línea '{Sku}' de la Orden de Compra '{ExternalId}' en Aurora.", articleSku, externalId);
            throw new PurchaseOrderAuroraApiException(
                externalId, $"Error de conexión al editar la línea '{articleSku}' de la Orden de Compra '{externalId}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} editando línea '{Sku}' de la Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, articleSku, externalId, response.Content);
            throw new PurchaseOrderAuroraApiException(externalId, $"Error editando línea '{articleSku}' de la Orden de Compra {externalId}: {response.StatusCode}");
        }
    }

    public async Task RemovePurchaseOrderArticleAsync(string externalId, string articleSku, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{externalId}/articles/{articleSku}", Method.Delete);
        AddWarehouseParameter(request, warehouse);

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Delete, $"{Endpoint}/{externalId}/articles/{articleSku}");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte eliminando línea '{Sku}' de la Orden de Compra '{ExternalId}' en Aurora.", articleSku, externalId);
            throw new PurchaseOrderAuroraApiException(
                externalId, $"Error de conexión al eliminar la línea '{articleSku}' de la Orden de Compra '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotente: si ya no está en Aurora, no hay nada que eliminar.
            return;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} eliminando línea '{Sku}' de la Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, articleSku, externalId, response.Content);
            throw new PurchaseOrderAuroraApiException(externalId, $"Error eliminando línea '{articleSku}' de la Orden de Compra {externalId}: {response.StatusCode}");
        }
    }

    public async Task CancelPurchaseOrderAsync(string externalId, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{externalId}", Method.Delete);
        AddWarehouseParameter(request, warehouse);

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Delete, $"{Endpoint}/{externalId}");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte cancelando Orden de Compra '{ExternalId}' en Aurora.", externalId);
            throw new PurchaseOrderAuroraApiException(
                externalId, $"Error de conexión al cancelar la Orden de Compra '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotente: si ya no está en Aurora (nunca se creó o ya fue cancelada/eliminada
            // en una corrida anterior), no hay nada que cancelar.
            return;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} cancelando Orden de Compra '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);
            throw new PurchaseOrderAuroraApiException(externalId, $"Error cancelando Orden de Compra {externalId}: {response.StatusCode}");
        }
    }

    private void AddWarehouseParameter(RestRequest request, string? warehouse)
    {
        var resolved = warehouse ?? _defaultWarehouse;
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            request.AddQueryParameter("warehouse", resolved);
        }
    }

    private static void ThrowIfTransientFailure(RestResponseBase response, Method method, string resource)
    {
        if (response.StatusCode == HttpStatusCode.RequestTimeout ||
            (int)response.StatusCode >= 500 ||
            response.StatusCode == 0)
        {
            throw new HttpRequestException(
                $"Aurora transient error {(int)response.StatusCode} en {method} {resource}: {response.Content}");
        }
    }
}
