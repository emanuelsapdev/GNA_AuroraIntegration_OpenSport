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
/// Implementa IPurchaseOrderLookupRepository consultando el recurso PurchaseOrders de
/// SAP B1 Service Layer (incluye DocumentLines en la respuesta, sin necesidad de $expand:
/// es una colección hija nativa del documento, igual que en Orders/Invoices).
/// Fuente de verdad de negocio para PurchaseOrder antes de replicar hacia Aurora.
/// </summary>
public sealed class PurchaseOrderServiceLayerLookupRepository : IPurchaseOrderLookupRepository
{
    // Tamaño de sub-lote para $filter con múltiples "or" — evita URLs demasiado largas.
    private const int FilterBatchSize = 20;

    private readonly IServiceLayerClient _client;
    private readonly ILogger<PurchaseOrderServiceLayerLookupRepository> _logger;

    public PurchaseOrderServiceLayerLookupRepository(
        IServiceLayerClient client,
        ILogger<PurchaseOrderServiceLayerLookupRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<PurchaseOrder?> GetByDocEntryAsync(string docEntry, CancellationToken ct = default)
    {
        if (!TryParseDocEntry(docEntry, out int parsed))
        {
            _logger.LogWarning("DocEntry '{DocEntry}' no es un entero válido; se omite la consulta a Service Layer.", docEntry);
            return null;
        }

        var doc = await _client.GetAsync<ServiceLayerPurchaseOrderDto>(
            $"{SapB1PurchaseOrdersConstants.PurchaseOrders.Endpoint}({parsed})", ct);

        return doc is null ? null : MapToPurchaseOrder(doc);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetByDocEntryListAsync(
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
            return Array.Empty<PurchaseOrder>();

        // Mapa de DocEntry (int) → queueCode para adjuntarlo a la entidad resultante.
        var queueCodeByDocEntry = validEntries
            .ToDictionary(e => e.value, e => e.key.Item1);

        var docEntryList = validEntries.Select(e => e.value).ToList();
        var result = new List<PurchaseOrder>(docEntryList.Count);

        foreach (var batch in Chunk(docEntryList, FilterBatchSize))
        {
            var filter = string.Join(" or ",
                batch.Select(docEntry => $"{SapB1PurchaseOrdersConstants.PurchaseOrders.DocEntryField} eq {docEntry}"));

            string fields = $"{SapB1PurchaseOrdersConstants.PurchaseOrders.DocEntryField}," +
                            $"{SapB1PurchaseOrdersConstants.PurchaseOrders.DocNumField}," +
                            $"{SapB1PurchaseOrdersConstants.PurchaseOrders.CancelledField}," +
                            $"{SapB1PurchaseOrdersConstants.PurchaseOrders.DocumentLinesField}";

            var resource = $"{SapB1PurchaseOrdersConstants.PurchaseOrders.Endpoint}?$filter={filter}" +
                            $"&$select={fields}";

            var response = await _client.GetAsync<ServiceLayerCollectionDto<ServiceLayerPurchaseOrderDto>>(resource, ct);

            if (response?.Value.Count == 0)
            {
                _logger.LogWarning("Consulta de PurchaseOrders en Service Layer no devolvió resultados para el lote actual.");
                continue;
            }

            result.AddRange(response!.Value.Select(dto =>
            {
                queueCodeByDocEntry.TryGetValue(dto.DocEntry, out var queueCode);
                return MapToPurchaseOrder(dto, queueCode);
            }));
        }

        return result.AsReadOnly();
    }

    private static PurchaseOrder MapToPurchaseOrder(ServiceLayerPurchaseOrderDto dto, string? queueCode = null) => new()
    {
        DocEntry = dto.DocEntry,
        DocNum = dto.DocNum,
        Cancelled = string.Equals(
            dto.Cancelled, SapB1PurchaseOrdersConstants.PurchaseOrders.CancelledYesValue, StringComparison.OrdinalIgnoreCase),
        Lines = [.. dto.DocumentLines.Select(line => new PurchaseOrderLine
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

/// <summary>DTO interno del recurso PurchaseOrders de Service Layer (subset de campos usados).</summary>
internal sealed class ServiceLayerPurchaseOrderDto
{
    public int DocEntry { get; set; }
    public int? DocNum { get; set; }

    /// <summary>"tYES"/"tNO" (BoYesNoEnum). Ver SapB1PurchaseOrdersConstants.CancelledField.</summary>
    public string? Cancelled { get; set; }

    public List<ServiceLayerPurchaseOrderLineDto> DocumentLines { get; set; } = [];
}

internal sealed class ServiceLayerPurchaseOrderLineDto
{
    public int LineNum { get; set; }
    public string ItemCode { get; set; } = default!;
    public decimal Quantity { get; set; }
}


