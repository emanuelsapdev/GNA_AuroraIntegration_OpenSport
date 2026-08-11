using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound;

/// <summary>
/// Caso de uso: toma Solicitudes de Traslado pendientes de SAP B1 (Alta, Modificación o
/// Cancelación) y las refleja en Aurora WMS como "transfer out orders".
///
/// - Si la orden está cancelada en SAP (TransferOutOrder.Cancelled, ver
///   InventoryTransferRequestServiceLayerLookupRepository): se cancela en Aurora (DELETE), o
///   no se hace nada si nunca llegó a existir allí. Esta rama tiene prioridad sobre
///   Alta/Modificación: una orden cancelada nunca se crea ni se reconcilia, sin importar con
///   qué Operation haya quedado encolada la entrada.
/// - Si no está cancelada y no existe todavía en Aurora (chequeo vía GET): se crea completa.
/// - Si no está cancelada y ya existe: se reconcilia el estado actual de SAP contra el de
///   Aurora (GET .../articles), con una diferencia importante respecto a PurchaseOrder:
///
///   ⚠️ La API de Aurora NO expone POST .../transfer-out-orders/{externalId}/articles (alta
///   de artículos sobre una orden existente) — a diferencia de purchase-orders y sale-orders,
///   que sí lo tienen. Solo existen PATCH (editar línea existente) y DELETE (eliminar línea)
///   a nivel de artículo. Por lo tanto:
///     • líneas nuevas en SAP  → NO se pueden agregar vía API; se loguea una advertencia y se
///       omiten (limitación real de la API de Aurora, no una decisión de negocio).
///     • líneas presentes en ambos lados con cantidad distinta → se editan (PATCH .../articles/{sku})
///     • líneas que ya no están en SAP → se eliminan (DELETE .../articles/{sku})
///   Las líneas con fulfilledQuantity > 0 en Aurora (ya recibidas/en proceso en el depósito)
///   NUNCA se editan ni se eliminan — se loguea una advertencia y se continúa, para no
///   interferir con mercadería que el depósito ya procesó.
///
/// Fuera de alcance todavía (backlog):
///   - Sincronización de campos de header vía PATCH .../transfer-out-orders/{externalId}
///     (bannerName/bannerExternalId/notes/etc.): Aurora sí expone este endpoint (a diferencia
///     de purchase-orders), pero documenta una precondición de estado ("Estado de la orden ->
///     PENDIENTE, CONGELADA. Utilizar estado TO_EDIT para modificar el pedido.") sin que exista
///     hoy una definición de negocio sobre cuándo disparar esa transición — no se implementa
///     para no inventar ese comportamiento. Ver IAuroraTransferOutOrderApiClient.UpdateTransferOutOrderHeaderAsync.
///   - logisticOperatorExternalId/postalCode/shippingPriorityExternalId en la creación: sin
///     campo SAP (OWTQ) mapeado hoy — ver CreateAuroraTransferOutOrderDto.
/// </summary>
public sealed class TransferOutOrderSyncUseCase : ITransferOutOrderSyncUseCase
{
    private readonly ITransferOutOrderReplicationRepository _repository;
    private readonly IAuroraTransferOutOrderApiClient _auroraClient;
    private readonly ITransferOutOrderPayloadValidator _validator;
    private readonly ILogger<TransferOutOrderSyncUseCase> _logger;

    public TransferOutOrderSyncUseCase(
        ITransferOutOrderReplicationRepository repository,
        IAuroraTransferOutOrderApiClient auroraClient,
        ITransferOutOrderPayloadValidator validator,
        ILogger<TransferOutOrderSyncUseCase> logger)
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
            IReadOnlyList<TransferOutOrder> pending = await _repository.GetPendingTransferOutOrdersAsync(batchSize: 100, ct);

            foreach (TransferOutOrder transferOutOrder in pending)
            {
                processed++;
                ct.ThrowIfCancellationRequested();

                string docEntry = transferOutOrder.DocEntry.ToString();

                try
                {
                    if (transferOutOrder.Cancelled)
                    {
                        await CancelInAuroraAsync(docEntry, ct);
                    }
                    else
                    {
                        AuroraTransferOutOrderDto? existing = await _auroraClient.GetTransferOutOrderByExternalIdAsync(docEntry, warehouse: null, ct);

                        if (existing is null)
                        {
                            CreateAuroraTransferOutOrderDto createDto = MapToCreateDto(transferOutOrder);
                            _validator.Validate(createDto);
                            await _auroraClient.CreateTransferOutOrderAsync(createDto, warehouse: null, ct);
                            _logger.LogInformation("Solicitud de Traslado '{DocEntry}' creada en Aurora.", docEntry);
                        }
                        else
                        {
                            await ReconcileLinesAsync(docEntry, transferOutOrder.Lines, ct);
                        }
                    }

                    await _repository.MarkTransferOutOrderAsReplicatedAsync(docEntry, ct);
                    successful++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replicando Solicitud de Traslado '{DocEntry}'", docEntry);
                    await _repository.MarkTransferOutOrderAsFailedAsync(docEntry, ex.Message, ct);
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
        AuroraTransferOutOrderDto? existing = await _auroraClient.GetTransferOutOrderByExternalIdAsync(externalId, warehouse: null, ct);

        if (existing is null)
        {
            _logger.LogInformation(
                "Solicitud de Traslado '{ExternalId}' está cancelada en SAP pero nunca existió en Aurora; no hay nada que cancelar.", externalId);
            return;
        }

        await _auroraClient.CancelTransferOutOrderAsync(externalId, warehouse: null, ct);
        _logger.LogInformation("Solicitud de Traslado '{ExternalId}' cancelada en Aurora.", externalId);
    }

    /// <summary>
    /// Reconcilia las líneas de una orden ya existente en Aurora contra el estado actual en
    /// SAP B1. Trabaja siempre sobre el estado completo (no un delta) — es indistinto si la
    /// entrada de la cola quedó marcada como 'I' o 'U': el resultado final refleja el SAP de
    /// hoy, salvo la limitación de líneas nuevas descripta abajo.
    /// </summary>
    private async Task ReconcileLinesAsync(string externalId, IReadOnlyList<TransferOutOrderLine> sapLines, CancellationToken ct)
    {
        IReadOnlyList<TransferOutOrderArticleStateDto> auroraLines =
            await _auroraClient.GetTransferOutOrderArticlesAsync(externalId, warehouse: null, ct);

        Dictionary<string, TransferOutOrderArticleStateDto> auroraBySku =
            auroraLines.ToDictionary(line => line.ArticleSku, StringComparer.OrdinalIgnoreCase);
        HashSet<string> sapSkus = new(sapLines.Select(line => line.ArticleSku), StringComparer.OrdinalIgnoreCase);

        // 1) Líneas nuevas en SAP que Aurora todavía no tiene.
        // ⚠️ Limitación de la API de Aurora (no de negocio): transfer-out-orders no expone un
        // POST .../articles para agregar líneas a una orden existente. Se loguea y se omite —
        // ver el comentario de clase para el detalle completo.
        List<TransferOutOrderLine> newLines = [.. sapLines.Where(line => !auroraBySku.ContainsKey(line.ArticleSku))];
        if (newLines.Count > 0)
        {
            _logger.LogWarning(
                "Solicitud de Traslado '{ExternalId}': {Count} línea(s) nueva(s) en SAP ({Skus}) no se pueden agregar en Aurora — la API no expone alta de artículos sobre una orden de transferencia de salida existente.",
                externalId, newLines.Count, string.Join(", ", newLines.Select(line => line.ArticleSku)));
        }

        // 2) Líneas presentes en ambos lados con cantidad distinta.
        foreach (TransferOutOrderLine sapLine in sapLines)
        {
            if (!auroraBySku.TryGetValue(sapLine.ArticleSku, out TransferOutOrderArticleStateDto? auroraLine))
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

            await _auroraClient.UpdateTransferOutOrderArticleAsync(externalId, sapLine.ArticleSku, MapLine(sapLine), warehouse: null, ct);
            _logger.LogInformation(
                "Solicitud de Traslado '{ExternalId}': línea '{Sku}' actualizada a cantidad {Quantity} en Aurora.",
                externalId, sapLine.ArticleSku, sapQuantity);
        }

        // 3) Líneas que Aurora tiene pero que ya no están en SAP.
        foreach (TransferOutOrderArticleStateDto auroraLine in auroraLines)
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

            await _auroraClient.RemoveTransferOutOrderArticleAsync(externalId, auroraLine.ArticleSku, warehouse: null, ct);
            _logger.LogInformation("Solicitud de Traslado '{ExternalId}': línea '{Sku}' eliminada en Aurora.", externalId, auroraLine.ArticleSku);
        }
    }

    private static CreateAuroraTransferOutOrderDto MapToCreateDto(TransferOutOrder transferOutOrder) => new()
    {
        ExternalId = transferOutOrder.DocEntry.ToString(),
        BannerName = transferOutOrder.BannerName,
        BannerExternalId = transferOutOrder.BannerExternalId,
        Notes = transferOutOrder.Notes,
        Articles = [.. transferOutOrder.Lines.Select(MapLine)]
    };

    private static TransferOutOrderArticleDto MapLine(TransferOutOrderLine line) => new()
    {
        LineOrder = line.LineOrder,
        ArticleSku = line.ArticleSku,
        Quantity = ToAuroraQuantity(line.Quantity)
    };

    // Aurora espera "quantity" como entero; SAP B1 permite cantidades decimales en WTQ1
    // (ej. UoM por peso). Se redondea al mapear — ver Advertencias en PROJECT_PROGRESS.md.
    private static int ToAuroraQuantity(decimal quantity)
        => (int)Math.Round(quantity, MidpointRounding.AwayFromZero);
}
