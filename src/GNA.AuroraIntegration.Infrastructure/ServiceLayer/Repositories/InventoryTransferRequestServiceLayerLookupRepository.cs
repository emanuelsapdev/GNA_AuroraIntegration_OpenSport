using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.DTOs;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Repositories;

/// <summary>
/// Implementa IInventoryTransferRequestLookupRepository consultando el recurso
/// InventoryTransferRequests de SAP B1 Service Layer (incluye DocumentLines en la respuesta,
/// sin necesidad de $expand: es una colección hija nativa del documento, igual que en
/// Orders/PurchaseOrders). Fuente de verdad de negocio para InventoryTransferRequest antes de
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

    public async Task<InventoryTransferRequest?> GetByDocEntryAsync(string docEntry, CancellationToken ct = default)
    {
        if (!TryParseDocEntry(docEntry, out int parsed))
        {
            _logger.LogWarning("DocEntry '{DocEntry}' no es un entero válido; se omite la consulta a Service Layer.", docEntry);
            return null;
        }

        var doc = await _client.GetAsync<ServiceLayerInventoryTransferRequestDto>(
            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.Endpoint}({parsed})", ct);

        return doc is null ? null : MapToInventoryTransferRequest(doc);
    }

    public async Task<IReadOnlyList<InventoryTransferRequest>> GetByDocEntryListAsync(
        IEnumerable<(string, string)> docEntries, CancellationToken ct = default)
    {
        var parsedEntries = docEntries
            .Distinct()
            .Select(key => (key, ok: TryParseDocEntry(key.Item2, out int value), value))
            .ToList();

        foreach (var invalid in parsedEntries.Where(e => !e.ok))
        {
            _logger.LogWarning("DocEntry '{DocEntry}' no es un entero válido; se omite del lote.", invalid.key);
        }

        var validEntries = parsedEntries.Where(e => e.ok).ToList();
        if (validEntries.Count == 0)
            return Array.Empty<InventoryTransferRequest>();

        // Mapa de DocEntry (int) → queueCode para adjuntarlo a la entidad resultante.
        var queueCodeByDocEntry = validEntries
            .ToDictionary(e => e.value, e => e.key.Item1);

        var docEntryList = validEntries.Select(e => e.value).ToList();
        var result = new List<InventoryTransferRequest>(docEntryList.Count);

        foreach (var batch in Chunk(docEntryList, FilterBatchSize))
        {
            var filter = string.Join(" or ",
                batch.Select(docEntry => $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocEntryField} eq {docEntry}"));

            string fields = $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocEntryField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocNumField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.TypeClosureField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocumentStatusField}," +
                            $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocumentLinesField}";

            var resource = $"{SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.Endpoint}?$filter={filter}" +
                            $"&$select={fields}";

            var response = await _client.GetAsync<ServiceLayerCollectionDto<ServiceLayerInventoryTransferRequestDto>>(resource, ct);

            if (response?.Value.Count == 0)
            {
                _logger.LogWarning("Consulta de InventoryTransferRequests en Service Layer no devolvió resultados para el lote actual.");
                continue;
            }

            result.AddRange(response!.Value.Select(dto =>
            {
                queueCodeByDocEntry.TryGetValue(dto.DocEntry, out var queueCode);
                return MapToInventoryTransferRequest(dto, queueCode);
            }));
        }

        return result.AsReadOnly();
    }

    private static InventoryTransferRequest MapToInventoryTransferRequest(ServiceLayerInventoryTransferRequestDto dto, string? queueCode = null) => new()
    {
        DocEntry = dto.DocEntry,
        DocNum = dto.DocNum,
        IsClosedManual = string.Equals(
            dto.TypeClosure, SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.TypeClosureManualValue, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            dto.DocumentStatus, SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.DocumentStatusCloseValue, StringComparison.OrdinalIgnoreCase),
        Lines = [.. dto.DocumentLines.Select(line => new InventoryTransferRequestLine
        {
            LineOrder = line.LineNum,
            ArticleSku = line.ItemCode,
            Quantity = line.Quantity
        })],
        QueueCode = queueCode
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
    public string? DocumentStatus { get; set; }

    [JsonPropertyName(SapB1InventoryTransferRequestsConstants.InventoryTransferRequests.TypeClosureField)]
    public string? TypeClosure { get; set; }

    public List<ServiceLayerInventoryTransferRequestLineDto> DocumentLines { get; set; } = [];
}

internal sealed class ServiceLayerInventoryTransferRequestLineDto
{
    public int LineNum { get; set; }
    public string ItemCode { get; set; } = default!;
    public decimal Quantity { get; set; }
}
