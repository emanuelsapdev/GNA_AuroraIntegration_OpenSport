using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.DTOs;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Repositories;

/// <summary>
/// Implementa IInventoryTransferRequestLookupRepository consultando el recurso
/// InventoryTransferRequests de SAP B1 Service Layer (incluye DocumentLines en la respuesta,
/// sin necesidad de $expand: es una colección hija nativa del documento, igual que en
/// Orders/PurchaseOrders). Fuente de verdad de negocio para TransferOutOrder antes de
/// replicar hacia Aurora.
/// </summary>
public sealed class InventoryTransferRequestServiceLayerLookupRepository : IInventoryTransferRequestLookupRepository
{
    // Tamaño de sub-lote para $filter con múltiples "or" — evita URLs demasiado largas.
    private const int FilterBatchSize = 20;

    private readonly IServiceLayerClient _client;
    private readonly ILogger<InventoryTransferRequestServiceLayerLookupRepository> _logger;

    public InventoryTransferRequestServiceLayerLookupRepository(
        IServiceLayerClient client,
        ILogger<InventoryTransferRequestServiceLayerLookupRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<TransferOutOrder?> GetByDocEntryAsync(string docEntry, CancellationToken ct = default)
    {
        if (!TryParseDocEntry(docEntry, out int parsed))
        {
            _logger.LogWarning("DocEntry '{DocEntry}' no es un entero válido; se omite la consulta a Service Layer.", docEntry);
            return null;
        }

        var doc = await _client.GetAsync<ServiceLayerInventoryTransferRequestDto>(
            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.Endpoint}({parsed})", ct);

        return doc is null ? null : MapToTransferOutOrder(doc);
    }

    public async Task<IReadOnlyList<TransferOutOrder>> GetByDocEntryListAsync(
        IEnumerable<string> docEntries, CancellationToken ct = default)
    {
        var parsedEntries = docEntries
            .Distinct()
            .Select(key => (key, ok: TryParseDocEntry(key, out int value), value))
            .ToList();

        foreach (var invalid in parsedEntries.Where(e => !e.ok))
        {
            _logger.LogWarning("DocEntry '{DocEntry}' no es un entero válido; se omite del lote.", invalid.key);
        }

        var docEntryList = parsedEntries.Where(e => e.ok).Select(e => e.value).ToList();
        if (docEntryList.Count == 0)
            return Array.Empty<TransferOutOrder>();

        var result = new List<TransferOutOrder>(docEntryList.Count);

        foreach (var batch in Chunk(docEntryList, FilterBatchSize))
        {
            var filter = string.Join(" or ",
                batch.Select(docEntry => $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocEntryField} eq {docEntry}"));

            string fields = $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocEntryField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocNumField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.CancelledField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocumentLinesField}";

            var resource = $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.Endpoint}?$filter={filter}" +
                            $"&$select={fields}";

            var response = await _client.GetAsync<ServiceLayerCollectionDto<ServiceLayerInventoryTransferRequestDto>>(resource, ct);

            if (response?.Value.Count == 0)
            {
                _logger.LogWarning("Consulta de InventoryTransferRequests en Service Layer no devolvió resultados para el lote actual.");
                continue;
            }

            result.AddRange(response?.Value.Select(MapToTransferOutOrder)!);
        }

        return result.AsReadOnly();
    }

    private static TransferOutOrder MapToTransferOutOrder(ServiceLayerInventoryTransferRequestDto dto) => new()
    {
        DocEntry = dto.DocEntry,
        DocNum = dto.DocNum,
        Cancelled = string.Equals(
            dto.Cancelled, SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.CancelledYesValue, StringComparison.OrdinalIgnoreCase),
        Lines = [.. dto.DocumentLines.Select(line => new TransferOutOrderLine
        {
            LineOrder = line.LineNum,
            ArticleSku = line.ItemCode,
            Quantity = line.Quantity
        })]
    };

    private static bool TryParseDocEntry(string docEntry, out int value)
        => int.TryParse(docEntry, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static IEnumerable<List<int>> Chunk(List<int> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}

/// <summary>DTO interno del recurso InventoryTransferRequests de Service Layer (subset de campos usados).</summary>
internal sealed class ServiceLayerInventoryTransferRequestDto
{
    public int DocEntry { get; set; }
    public int? DocNum { get; set; }

    /// <summary>"tYES"/"tNO" (BoYesNoEnum). Ver SapB1InventoryTransferRequestsConstants.CancelledField.</summary>
    public string? Cancelled { get; set; }

    public List<ServiceLayerInventoryTransferRequestLineDto> DocumentLines { get; set; } = [];
}

internal sealed class ServiceLayerInventoryTransferRequestLineDto
{
    public int LineNum { get; set; }
    public string ItemCode { get; set; } = default!;
    public decimal Quantity { get; set; }
}
