using GNA.AuroraIntegration.Application.DTOs.Aurora;

namespace GNA.AuroraIntegration.Application.Interfaces;

/// <summary>
/// Cliente hacia el recurso /aurora-erp/transfer-out-orders. A diferencia de
/// IAuroraPurchaseOrderApiClient, NO expone un método de "agregar artículos" (AddArticles):
/// la API de Aurora no tiene un POST .../articles para este recurso, solo PATCH/DELETE a
/// nivel de línea existente. Ver TransferOutOrderSyncUseCase.ReconcileLinesAsync para el
/// manejo de esta limitación (líneas nuevas no reconciliables automáticamente).
/// </summary>
public interface IAuroraTransferOutOrderApiClient
{
    Task CreateTransferOutOrderAsync(CreateAuroraTransferOutOrderDto transferOutOrder, string? warehouse, CancellationToken ct = default);
    Task<AuroraTransferOutOrderDto?> GetTransferOutOrderByExternalIdAsync(string externalId, string? warehouse, CancellationToken ct = default);

    /// <summary>Cancela (DELETE) una orden existente en Aurora. Idempotente: no falla si ya no existe.</summary>
    Task CancelTransferOutOrderAsync(string externalId, string? warehouse, CancellationToken ct = default);

    /// <summary>Estado actual de las líneas de una orden en Aurora (cantidad solicitada vs. cumplida).</summary>
    Task<IReadOnlyList<TransferOutOrderArticleStateDto>> GetTransferOutOrderArticlesAsync(string externalId, string? warehouse, CancellationToken ct = default);

    /// <summary>Edita cantidad/orden de una línea existente de la orden en Aurora.</summary>
    Task UpdateTransferOutOrderArticleAsync(string externalId, string articleSku, TransferOutOrderArticleDto article, string? warehouse, CancellationToken ct = default);

    /// <summary>Elimina una línea de la orden en Aurora.</summary>
    Task RemoveTransferOutOrderArticleAsync(string externalId, string articleSku, string? warehouse, CancellationToken ct = default);

    /// <summary>
    /// Modifica campos de cabecera (PATCH .../transfer-out-orders/{externalId}). Expuesta por
    /// completitud del contrato, pero no invocada automáticamente por
    /// TransferOutOrderSyncUseCase — ver UpdateAuroraTransferOutOrderDto.
    /// </summary>
    Task UpdateTransferOutOrderHeaderAsync(string externalId, UpdateAuroraTransferOutOrderDto header, string? warehouse, CancellationToken ct = default);
}
