using GNA.AuroraIntegration.Application.DTOs.Aurora.InventoryTransferRequest;
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
/// Cliente HTTP hacia los endpoints de Órdenes de Transferencia de Salida
/// ("transfer-out-orders") de Aurora WMS. Mismo patrón de resiliencia (retry + circuit
/// breaker) que AuroraPurchaseOrderApiClient.
///
/// ⚠️ A diferencia de AuroraPurchaseOrderApiClient, este cliente NO expone un método de
/// "agregar artículos": la API de Aurora no tiene POST .../transfer-out-orders/{externalId}/articles.
/// Sí expone, en cambio, UpdateInventoryTransferRequestHeaderAsync (PATCH de cabecera), que
/// purchase-orders no tiene — ver comentario en IAuroraInventoryTransferRequestApiClient.
/// </summary>
public sealed class AuroraInventoryTransferRequestApiClient : IAuroraInventoryTransferRequestApiClient
{
    private const string Endpoint = "aurora-erp/transfer-out-orders";

    private readonly RestClient _client;
    private readonly AsyncPolicy _resiliencePolicy;
    private readonly ILogger<AuroraInventoryTransferRequestApiClient> _logger;
    private readonly string? _defaultWarehouse;

    public AuroraInventoryTransferRequestApiClient(IOptions<AuroraApiSettings> settings, ILogger<AuroraInventoryTransferRequestApiClient> logger)
    {
        _logger = logger;

        AuroraApiSettings cfg = settings.Value;
        _defaultWarehouse = cfg.Warehouse;

        if (string.IsNullOrWhiteSpace(_defaultWarehouse))
        {
            // No es fatal: se documenta como TODO en AuroraApiSettings. Se loguea acá porque
            // Aurora exige "warehouse" en estos endpoints (a diferencia de Artículos) y sin él
            // es probable que las creaciones de la orden sean rechazadas.
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

    public async Task CreateInventoryTransferRequestAsync(CreateAuroraInventoryTransferRequestDto InventoryTransferRequest, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new(Endpoint, Method.Post);

        AddWarehouseParameter(request, warehouse);

        var json = JsonSerializer.Serialize(InventoryTransferRequest);
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
                "Error de transporte creando Solicitud de Traslado '{ExternalId}' en Aurora.", InventoryTransferRequest.ExternalId);

            throw new InventoryTransferRequestAuroraApiException(
                InventoryTransferRequest.ExternalId, $"Error de conexión al crear la Solicitud de Traslado '{InventoryTransferRequest.ExternalId}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} creando Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, InventoryTransferRequest.ExternalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(
                InventoryTransferRequest.ExternalId, $"[POST] Error creando Solicitud de Traslado {InventoryTransferRequest.ExternalId}: {auroraMessage}");
        }
    }

    public async Task<AuroraInventoryTransferRequestDto?> GetInventoryTransferRequestByExternalIdAsync(string externalId, string? warehouse, CancellationToken ct = default)
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
                "Error de transporte obteniendo Solicitud de Traslado '{ExternalId}' en Aurora.", externalId);
            throw new InventoryTransferRequestAuroraApiException(
                externalId, $"Error de conexión al obtener la Solicitud de Traslado '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} obteniendo Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(externalId, $"[GET] Error obteniendo Solicitud de Traslado {externalId}: {auroraMessage}");
        }

        return JsonSerializer.Deserialize<AuroraInventoryTransferRequestDto?>(response.Content ?? string.Empty);
    }

    public async Task<IReadOnlyList<InventoryTransferRequestArticleStateDto>> GetInventoryTransferRequestArticlesAsync(string externalId, string? warehouse, CancellationToken ct = default)
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
                "Error de transporte obteniendo líneas de la Solicitud de Traslado '{ExternalId}' en Aurora.", externalId);
            throw new InventoryTransferRequestAuroraApiException(
                externalId, $"Error de conexión al obtener líneas de la Solicitud de Traslado '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} obteniendo líneas de la Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(externalId, $"[GET] Error obteniendo líneas de la Solicitud de Traslado {externalId}: {auroraMessage}");
        }

        return JsonSerializer.Deserialize<List<InventoryTransferRequestArticleStateDto>>(response.Content ?? "[]") ?? [];
    }

    public async Task UpdateInventoryTransferRequestArticleAsync(string externalId, string articleSku, InventoryTransferRequestArticleDto article, string? warehouse, CancellationToken ct = default)
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
                "Error de transporte editando línea '{Sku}' de la Solicitud de Traslado '{ExternalId}' en Aurora.", articleSku, externalId);
            throw new InventoryTransferRequestAuroraApiException(
                externalId, $"Error de conexión al editar la línea '{articleSku}' de la Solicitud de Traslado '{externalId}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} editando línea '{Sku}' de la Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, articleSku, externalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(externalId, $"[PATCH] Error editando línea '{articleSku}' de la Solicitud de Traslado {externalId}: {auroraMessage}");
        }
    }

    public async Task RemoveInventoryTransferRequestArticleAsync(string externalId, string articleSku, string? warehouse, CancellationToken ct = default)
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
                "Error de transporte eliminando línea '{Sku}' de la Solicitud de Traslado '{ExternalId}' en Aurora.", articleSku, externalId);
            throw new InventoryTransferRequestAuroraApiException(
                externalId, $"Error de conexión al eliminar la línea '{articleSku}' de la Solicitud de Traslado '{externalId}' en Aurora.", ex);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotente: si ya no está en Aurora, no hay nada que eliminar.
            return;
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} eliminando línea '{Sku}' de la Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, articleSku, externalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(externalId, $"[DELETE] Error eliminando línea '{articleSku}' de la Solicitud de Traslado {externalId}: {auroraMessage}");
        }
    }

    public async Task CancelInventoryTransferRequestAsync(string externalId, string? warehouse, CancellationToken ct = default)
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
                "Error de transporte cancelando Solicitud de Traslado '{ExternalId}' en Aurora.", externalId);
            throw new InventoryTransferRequestAuroraApiException(
                externalId, $"Error de conexión al cancelar la Solicitud de Traslado '{externalId}' en Aurora.", ex);
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
                "Aurora API error {StatusCode} cancelando Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(externalId, $"[DELETE] Error cancelando Solicitud de Traslado {externalId}: {auroraMessage}");
        }
    }

    public async Task UpdateInventoryTransferRequestHeaderAsync(string externalId, UpdateAuroraInventoryTransferRequestDto header, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{externalId}", Method.Patch);
        AddWarehouseParameter(request, warehouse);
        request.AddJsonBody(JsonSerializer.Serialize(header));

        RestResponse response;
        try
        {
            response = await _resiliencePolicy.ExecuteAsync(async innerCt =>
            {
                RestResponse transientResponse = await _client.ExecuteAsync(request, innerCt);
                ThrowIfTransientFailure(transientResponse, Method.Patch, $"{Endpoint}/{externalId}");
                return transientResponse;
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte modificando cabecera de la Solicitud de Traslado '{ExternalId}' en Aurora.", externalId);
            throw new InventoryTransferRequestAuroraApiException(
                externalId, $"Error de conexión al modificar la cabecera de la Solicitud de Traslado '{externalId}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} modificando cabecera de la Solicitud de Traslado '{ExternalId}': {Body}",
                response.StatusCode, externalId, response.Content);

            string auroraMessage = AuroraApiErrorMessageExtractor.GetErrorMessageOrStatusCode(response.Content, response.StatusCode);
            throw new InventoryTransferRequestAuroraApiException(externalId, $"[PATCH] Error modificando cabecera de la Solicitud de Traslado {externalId}: {auroraMessage}");
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
