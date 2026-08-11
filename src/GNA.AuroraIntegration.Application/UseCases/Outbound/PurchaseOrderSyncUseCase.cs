using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound;

/// <summary>
/// Caso de uso: toma Órdenes de Compra pendientes de SAP B1 (Alta o Modificación) y las
/// refleja en Aurora WMS.
///
/// - Si la OC no existe todavía en Aurora (chequeo vía GET): se crea completa.
/// - Si ya existe: Aurora no expone un PATCH de header para "purchase-orders" (a diferencia
///   de "sale-orders"), así que la única forma de reflejar una modificación es a nivel línea.
///   Se reconcilia el estado actual de SAP contra el de Aurora (GET .../articles):
///     • líneas nuevas en SAP  → se agregan (POST .../articles)
///     • líneas con cantidad distinta → se editan (PATCH .../articles/{sku})
///     • líneas que ya no están en SAP → se eliminan (DELETE .../articles/{sku})
///   Las líneas con fulfilledQuantity > 0 en Aurora (ya recibidas/en proceso en el depósito)
///   NUNCA se editan ni se eliminan — se loguea una advertencia y se continúa, para no
///   interferir con mercadería que el depósito ya procesó.
///
/// Fuera de alcance todavía (backlog): Cancelación de OC, y sincronización de campos de
/// header (bannerName/bannerExternalId/notes) — Aurora no expone endpoint para esto último
/// en purchase-orders.
/// </summary>
public sealed class PurchaseOrderSyncUseCase : IPurchaseOrderSyncUseCase
{
    private readonly IPurchaseOrderReplicationRepository _repository;
    private readonly IAuroraPurchaseOrderApiClient _auroraClient;
    private readonly IPurchaseOrderPayloadValidator _validator;
    private readonly ILogger<PurchaseOrderSyncUseCase> _logger;

    public PurchaseOrderSyncUseCase(
        IPurchaseOrderReplicationRepository repository,
        IAuroraPurchaseOrderApiClient auroraClient,
        IPurchaseOrderPayloadValidator validator,
        ILogger<PurchaseOrderSyncUseCase> logger)
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
            IReadOnlyList<PurchaseOrder> pending = await _repository.GetPendingPurchaseOrdersAsync(batchSize: 100, ct);

            foreach (PurchaseOrder purchaseOrder in pending)
            {
                processed++;
                ct.ThrowIfCancellationRequested();

                string docEntry = purchaseOrder.DocEntry.ToString();

                try
                {
                    AuroraPurchaseOrderDto? existing = await _auroraClient.GetPurchaseOrderByExternalIdAsync(docEntry, warehouse: null, ct);

                    if (existing is null)
                    {
                        CreateAuroraPurchaseOrderDto createDto = MapToCreateDto(purchaseOrder);
                        _validator.Validate(createDto);
                        await _auroraClient.CreatePurchaseOrderAsync(createDto, warehouse: null, ct);
                        _logger.LogInformation("Orden de Compra '{DocEntry}' creada en Aurora.", docEntry);
                    }
                    else
                    {
                        await ReconcileLinesAsync(docEntry, purchaseOrder.Lines, ct);
                    }

                    await _repository.MarkPurchaseOrderAsReplicatedAsync(docEntry, ct);
                    successful++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replicando Orden de Compra '{DocEntry}'", docEntry);
                    await _repository.MarkPurchaseOrderAsFailedAsync(docEntry, ex.Message, ct);
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
            _logger.LogError(ex, "Error al obtener Órdenes de Compra pendientes del repositorio");
            throw;
        }

        return (processed, successful, failed);
    }

    /// <summary>
    /// Reconcilia las líneas de una OC ya existente en Aurora contra el estado actual en SAP B1.
    /// Trabaja siempre sobre el estado completo (no un delta) — es indistinto si la entrada de
    /// la cola quedó marcada como 'I' o 'U': el resultado final refleja el SAP de hoy.
    /// </summary>
    private async Task ReconcileLinesAsync(string externalId, IReadOnlyList<PurchaseOrderLine> sapLines, CancellationToken ct)
    {
        IReadOnlyList<PurchaseOrderArticleStateDto> auroraLines =
            await _auroraClient.GetPurchaseOrderArticlesAsync(externalId, warehouse: null, ct);

        Dictionary<string, PurchaseOrderArticleStateDto> auroraBySku =
            auroraLines.ToDictionary(line => line.ArticleSku, StringComparer.OrdinalIgnoreCase);
        HashSet<string> sapSkus = new(sapLines.Select(line => line.ArticleSku), StringComparer.OrdinalIgnoreCase);

        // 1) Líneas nuevas en SAP que Aurora todavía no tiene.
        List<PurchaseOrderArticleDto> toAdd = [.. sapLines
            .Where(line => !auroraBySku.ContainsKey(line.ArticleSku))
            .Select(MapLine)];

        if (toAdd.Count > 0)
        {
            await _auroraClient.AddPurchaseOrderArticlesAsync(externalId, toAdd, warehouse: null, ct);
            _logger.LogInformation("OC '{ExternalId}': {Count} línea(s) agregada(s) en Aurora.", externalId, toAdd.Count);
        }

        // 2) Líneas presentes en ambos lados con cantidad distinta.
        foreach (PurchaseOrderLine sapLine in sapLines)
        {
            if (!auroraBySku.TryGetValue(sapLine.ArticleSku, out PurchaseOrderArticleStateDto? auroraLine))
            {
                continue; // ya cubierta por el alta del paso 1
            }

            int sapQuantity = ToAuroraQuantity(sapLine.Quantity);
            if (auroraLine.RequestedQuantity == sapQuantity)
            {
                continue; // sin cambios
            }

            if (auroraLine.FulfilledQuantity > 0)
            {
                _logger.LogWarning(
                    "OC '{ExternalId}': la línea '{Sku}' cambió de cantidad en SAP ({SapQuantity}) pero ya tiene {Fulfilled} unidad(es) cumplida(s) en Aurora; se omite la edición.",
                    externalId, sapLine.ArticleSku, sapQuantity, auroraLine.FulfilledQuantity);
                continue;
            }

            await _auroraClient.UpdatePurchaseOrderArticleAsync(externalId, sapLine.ArticleSku, MapLine(sapLine), warehouse: null, ct);
            _logger.LogInformation(
                "OC '{ExternalId}': línea '{Sku}' actualizada a cantidad {Quantity} en Aurora.",
                externalId, sapLine.ArticleSku, sapQuantity);
        }

        // 3) Líneas que Aurora tiene pero que ya no están en SAP.
        foreach (PurchaseOrderArticleStateDto auroraLine in auroraLines)
        {
            if (sapSkus.Contains(auroraLine.ArticleSku))
            {
                continue;
            }

            if (auroraLine.FulfilledQuantity > 0)
            {
                _logger.LogWarning(
                    "OC '{ExternalId}': la línea '{Sku}' ya no está en SAP pero tiene {Fulfilled} unidad(es) cumplida(s) en Aurora; se omite la eliminación.",
                    externalId, auroraLine.ArticleSku, auroraLine.FulfilledQuantity);
                continue;
            }

            await _auroraClient.RemovePurchaseOrderArticleAsync(externalId, auroraLine.ArticleSku, warehouse: null, ct);
            _logger.LogInformation("OC '{ExternalId}': línea '{Sku}' eliminada en Aurora.", externalId, auroraLine.ArticleSku);
        }
    }

    private static CreateAuroraPurchaseOrderDto MapToCreateDto(PurchaseOrder purchaseOrder) => new()
    {
        ExternalId = purchaseOrder.DocEntry.ToString(),
        BannerName = purchaseOrder.BannerName,
        BannerExternalId = purchaseOrder.BannerExternalId,
        Notes = purchaseOrder.Notes,
        Articles = [.. purchaseOrder.Lines.Select(MapLine)]
    };

    private static PurchaseOrderArticleDto MapLine(PurchaseOrderLine line) => new()
    {
        LineOrder = line.LineOrder,
        ArticleSku = line.ArticleSku,
        Quantity = ToAuroraQuantity(line.Quantity)
    };

    // Aurora espera "quantity" como entero; SAP B1 permite cantidades decimales en POR1
    // (ej. UoM por peso). Se redondea al mapear — ver Advertencias en PROJECT_PROGRESS.md.
    private static int ToAuroraQuantity(decimal quantity)
        => (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
}
