using GNA.AuroraIntegration.Application.DTOs.Aurora.InventoryTransferRequest;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound;

public sealed class InventoryTransferRequestSyncUseCase : IInventoryTransferRequestSyncUseCase
{
    private readonly IInventoryTransferRequestReplicationRepository _repository;
    private readonly IAuroraInventoryTransferRequestApiClient _auroraClient;
    private readonly IInventoryTransferRequestPayloadValidator _validator;
    private readonly ILogger<InventoryTransferRequestSyncUseCase> _logger;

    public InventoryTransferRequestSyncUseCase(
        IInventoryTransferRequestReplicationRepository repository,
        IAuroraInventoryTransferRequestApiClient auroraClient,
        IInventoryTransferRequestPayloadValidator validator,
        ILogger<InventoryTransferRequestSyncUseCase> logger)
    {
        _repository = repository;
        _auroraClient = auroraClient;
        _validator = validator;
        _logger = logger;
    }

    public async Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default)
    {
        int processed = 0;
        int successful = 0;
        int failed = 0;

        try
        {
            IReadOnlyList<InventoryTransferRequest> pending = await _repository.GetPendingInventoryTransferRequestAsync(batchSize: 100, ct);

            foreach (InventoryTransferRequest InventoryTransferRequest in pending)
            {
                processed++;
                ct.ThrowIfCancellationRequested();

                string docEntry = InventoryTransferRequest.DocEntry.ToString();

                try
                {
                    if (InventoryTransferRequest.IsClosedManual)
                    {
                        await CancelInAuroraAsync(docEntry, ct);
                        _logger.LogInformation("Solicitud de Traslado '{DocEntry}' cancelada en Aurora.", docEntry);

                    }
                    else
                    {
                        AuroraInventoryTransferRequestDto? existing = await _auroraClient.GetInventoryTransferRequestByExternalIdAsync(docEntry, warehouse: null, ct);

                        if (existing is null)
                        {
                            CreateAuroraInventoryTransferRequestDto createDto = MapToCreateDto(InventoryTransferRequest);
                            _validator.Validate(createDto);
                            await _auroraClient.CreateInventoryTransferRequestAsync(createDto, warehouse: null, ct);
                            _logger.LogInformation("Solicitud de Traslado '{DocEntry}' creada en Aurora.", docEntry);
                        }
                        else
                        {
                            await ReconcileLinesAsync(docEntry, InventoryTransferRequest.Lines, ct);
                            _logger.LogInformation("Líneas de Solicitud de Traslado '{DocEntry}' conciliada en Aurora.", docEntry);

                        }
                    }

                    await _repository.MarkInventoryTransferRequestAsReplicatedAsync(docEntry, ct);
                    successful++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replicando Solicitud de Traslado '{DocEntry}'", docEntry);
                    await _repository.MarkInventoryTransferRequestAsFailedAsync(docEntry, ex.Message, ct);
                    failed++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener Solicitudes de Traslado pendientes del repositorio");
            throw;
        }

        return (processed, successful, failed);
    }

    /// <summary>
    /// Cancela la orden en Aurora si llegó a existir allí. Si nunca se creó (por ejemplo, fue
    /// cancelada en SAP antes de que el job de sincronización llegara a procesar el alta),
    /// no hay nada que cancelar y se trata como un no-op exitoso.
    /// </summary>
    private async Task CancelInAuroraAsync(string externalId, CancellationToken ct)
    {
        AuroraInventoryTransferRequestDto? existing = await _auroraClient.GetInventoryTransferRequestByExternalIdAsync(externalId, warehouse: null, ct);

        if (existing is null)
        {
            _logger.LogInformation(
                "Solicitud de Traslado '{ExternalId}' está cancelada en SAP pero nunca existió en Aurora; no hay nada que cancelar.", externalId);
            return;
        }

        await _auroraClient.CancelInventoryTransferRequestAsync(externalId, warehouse: null, ct);
        _logger.LogInformation("Solicitud de Traslado '{ExternalId}' cancelada en Aurora.", externalId);
    }

    /// <summary>
    /// Reconcilia las líneas de una orden ya existente en Aurora contra el estado actual en
    /// SAP B1. Trabaja siempre sobre el estado completo (no un delta) — es indistinto si la
    /// entrada de la cola quedó marcada como 'I' o 'U': el resultado final refleja el SAP de
    /// hoy, salvo la limitación de líneas nuevas descripta abajo.
    /// </summary>
    private async Task ReconcileLinesAsync(string externalId, IReadOnlyList<InventoryTransferRequestLine> sapLines, CancellationToken ct)
    {
        IReadOnlyList<InventoryTransferRequestArticleStateDto> auroraLines =
            await _auroraClient.GetInventoryTransferRequestArticlesAsync(externalId, warehouse: null, ct);

        Dictionary<string, InventoryTransferRequestArticleStateDto> auroraBySku =
            auroraLines.ToDictionary(line => line.ArticleSku, StringComparer.OrdinalIgnoreCase);
        HashSet<string> sapSkus = new(sapLines.Select(line => line.ArticleSku), StringComparer.OrdinalIgnoreCase);

        // 1) Líneas nuevas en SAP que Aurora todavía no tiene.
        // ⚠️ Limitación de la API de Aurora (no de negocio): transfer-out-orders no expone un
        // POST .../articles para agregar líneas a una orden existente. Se loguea y se omite —
        // ver el comentario de clase para el detalle completo.
        List<InventoryTransferRequestLine> newLines = [.. sapLines.Where(line => !auroraBySku.ContainsKey(line.ArticleSku))];
        if (newLines.Count > 0)
        {
            _logger.LogWarning(
                "Solicitud de Traslado '{ExternalId}': {Count} línea(s) nueva(s) en SAP ({Skus}) no se pueden agregar en Aurora — la API no expone alta de artículos sobre una orden de transferencia de salida existente.",
                externalId, newLines.Count, string.Join(", ", newLines.Select(line => line.ArticleSku)));
        }

        // 2) Líneas presentes en ambos lados con cantidad distinta.
        foreach (InventoryTransferRequestLine sapLine in sapLines)
        {
            if (!auroraBySku.TryGetValue(sapLine.ArticleSku, out InventoryTransferRequestArticleStateDto? auroraLine))
            {
                continue; // línea nueva, ya cubierta (y advertida) en el paso 1
            }

            int sapQuantity = ToAuroraQuantity(sapLine.Quantity);
            if (auroraLine.RequestedQuantity == sapQuantity)
            {
                continue; // sin cambios
            }

            if (auroraLine.FulfilledQuantity > 0)
            {
                _logger.LogWarning(
                    "Solicitud de Traslado '{ExternalId}': la línea '{Sku}' cambió de cantidad en SAP ({SapQuantity}) pero ya tiene {Fulfilled} unidad(es) cumplida(s) en Aurora; se omite la edición.",
                    externalId, sapLine.ArticleSku, sapQuantity, auroraLine.FulfilledQuantity);
                continue;
            }

            await _auroraClient.UpdateInventoryTransferRequestArticleAsync(externalId, sapLine.ArticleSku, MapLine(sapLine), warehouse: null, ct);
            _logger.LogInformation(
                "Solicitud de Traslado '{ExternalId}': línea '{Sku}' actualizada a cantidad {Quantity} en Aurora.",
                externalId, sapLine.ArticleSku, sapQuantity);
        }

        // 3) Líneas que Aurora tiene pero que ya no están en SAP.
        foreach (InventoryTransferRequestArticleStateDto auroraLine in auroraLines)
        {
            if (sapSkus.Contains(auroraLine.ArticleSku))
            {
                continue;
            }

            if (auroraLine.FulfilledQuantity > 0)
            {
                _logger.LogWarning(
                    "Solicitud de Traslado '{ExternalId}': la línea '{Sku}' ya no está en SAP pero tiene {Fulfilled} unidad(es) cumplida(s) en Aurora; se omite la eliminación.",
                    externalId, auroraLine.ArticleSku, auroraLine.FulfilledQuantity);
                continue;
            }

            await _auroraClient.RemoveInventoryTransferRequestArticleAsync(externalId, auroraLine.ArticleSku, warehouse: null, ct);
            _logger.LogInformation("Solicitud de Traslado '{ExternalId}': línea '{Sku}' eliminada en Aurora.", externalId, auroraLine.ArticleSku);
        }
    }

    private static CreateAuroraInventoryTransferRequestDto MapToCreateDto(InventoryTransferRequest InventoryTransferRequest) => new()
    {
        ExternalId = InventoryTransferRequest.DocEntry.ToString(),
        BannerName = InventoryTransferRequest.BannerName,
        BannerExternalId = InventoryTransferRequest.BannerExternalId,
        Notes = InventoryTransferRequest.Notes,
        Articles = [.. InventoryTransferRequest.Lines.Select(MapLine)]
    };

    private static InventoryTransferRequestArticleDto MapLine(InventoryTransferRequestLine line) => new()
    {
        LineOrder = line.LineOrder + 1,
        ArticleSku = line.ArticleSku,
        Quantity = ToAuroraQuantity(line.Quantity)
    };

    // Aurora espera "quantity" como entero; SAP B1 permite cantidades decimales en WTQ1
    // (ej. UoM por peso). Se redondea al mapear — ver Advertencias en PROJECT_PROGRESS.md.
    private static int ToAuroraQuantity(decimal quantity)
        => (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
}
