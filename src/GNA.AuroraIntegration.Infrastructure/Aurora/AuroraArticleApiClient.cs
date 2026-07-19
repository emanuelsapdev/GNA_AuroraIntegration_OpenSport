using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Infrastructure.Aurora;

public sealed class AuroraArticleApiClient : IAuroraArticleApiClient
{
    private const string Endpoint = "aurora-erp/articles";

    private readonly RestClient _client;
    private readonly ILogger<AuroraArticleApiClient> _logger;

    public AuroraArticleApiClient(IOptions<AuroraApiSettings> settings, ILogger<AuroraArticleApiClient> logger)
    {
        _logger = logger;

        AuroraApiSettings cfg = settings.Value;

        RestClientOptions options = new(cfg.BaseUrl.TrimEnd('/') + "/");

        _client = new RestClient(options);
        _client.AddDefaultHeader("X-Api-Key", cfg.ApiKey);
        _client.AddDefaultHeader("Accept", "application/json");
    }

    public async Task CreateArticleAsync(CreateArticleDto article, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new(Endpoint, Method.Post);

        if (warehouse is not null)
            request.AddQueryParameter("warehouse", warehouse);

        request.AddBody(article);

        RestResponse response;
        try
        {
            response = await _client.ExecuteAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte creando artículo '{Sku}' en Aurora.", article.Sku);

            throw new ArticleAuroraApiException(
                article.Sku, $"Error de conexión al crear el artículo '{article.Sku}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} creando artículo '{Sku}': {Body}",
                response.StatusCode, article.Sku, response.Content);

            throw new ArticleAuroraApiException(article.Sku, $"Error creando artículo {article.Sku}: {response.StatusCode}");
        }
    }

    public async Task UpdateArticleAsync(string sku, UpdateArticleDto article, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new(Endpoint, Method.Patch);

        if (warehouse is not null)
            request.AddQueryParameter("warehouse", warehouse);

        request.AddBody(article);

        RestResponse response;
        try
        {
            response = await _client.ExecuteAsync(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte actualizando artículo '{Sku}' en Aurora.", sku);

            throw new ArticleAuroraApiException(
                sku, $"Error de conexión al actualizar el artículo '{sku}' en Aurora.", ex);
        }

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} actualizando artículo '{Sku}': {Body}",
                response.StatusCode, sku, response.Content);

            throw new ArticleAuroraApiException(sku, $"Error actualizando artículo {sku}: {response.StatusCode}");
        }
    }

    public async Task GetArticleBySku(string sku, string? warehouse, CancellationToken ct = default)
    {
        RestRequest request = new($"{Endpoint}/{sku}", Method.Get);
        if (warehouse is not null)       
            request.AddQueryParameter("warehouse", warehouse);    
        RestResponse response;               
        try
        {
            response = await _client.ExecuteAsync(request, ct);    
        }
        catch (Exception ex) when (ex is not OperationCanceledException) 
        {
            _logger.LogError(ex,
                "Error de transporte obteniendo artículo '{Sku}' en Aurora.", sku);
            throw new ArticleAuroraApiException(
                sku, $"Error de conexión al obtener el artículo '{sku}' en Aurora.", ex);                           
        }
        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Aurora API error {StatusCode} obteniendo artículo '{Sku}': {Body}",
                response.StatusCode, sku, response.Content);
            throw new ArticleAuroraApiException(sku, $"Error obteniendo artículo {sku}: {response.StatusCode}");
        }
    }
}