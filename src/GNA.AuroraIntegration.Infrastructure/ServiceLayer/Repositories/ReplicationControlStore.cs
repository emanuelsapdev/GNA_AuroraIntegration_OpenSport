using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;
using Microsoft.Extensions.Logging;
using RestSharp;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Repositories;

/// <summary>
/// Implementa IReplicationControlStore sobre las UDTs de SAP B1 vía Service Layer.
///   @GNA_AUR_REP_QUEUE   – estado vivo de cada entidad pendiente.
///   @GNA_AUR_REP_ATTEMPT – histórico de intentos (OK y fallidos).
///
/// Las UDTs se exponen en Service Layer como /U_TABLENAME.
/// La clave primaria es el campo Code (string, máx. 8 chars alfanuméricos).
/// Se genera como los 8 primeros caracteres de un Guid para garantizar unicidad.
/// </summary>
public sealed class ReplicationControlStore : IReplicationControlStore
{
    private readonly IServiceLayerClient _client;
    private readonly ILogger<ReplicationControlStore> _logger;

    public ReplicationControlStore(
        IServiceLayerClient client,
        ILogger<ReplicationControlStore> logger)
    {
        _client = client;
        _logger = logger;
    }

    // ── Consulta ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<(string, string)>> GetPendingKeysAsync(
        ReplicableEntityType entityType, int batchSize = 100, CancellationToken ct = default)
    {
        const int pageSize = 25;
        var collected = new List<(string, string)>(batchSize);
        int skip = 0;
        string entityTypeLiteral = ReplicableEntityTypeMapper.ToServiceLayerLiteral(entityType);

        while (collected.Count < batchSize)
        {
            int remaining = batchSize - collected.Count;
            int top = Math.Min(pageSize, remaining);

            var resource = $"{SapB1ReplicationConstants.Queue.Endpoint}" +
                           $"?$filter={SapB1ReplicationConstants.Queue.EntityType} eq '{entityTypeLiteral}'" +
                           $" and ({SapB1ReplicationConstants.Queue.Status} eq '{SapB1ReplicationConstants.Queue.StatusValues.Pending}' " +
                           $" or ({SapB1ReplicationConstants.Queue.Status} eq '{SapB1ReplicationConstants.Queue.StatusValues.Failed}' and {SapB1ReplicationConstants.Queue.RetryCount} lt 4))" +
                           $"&$orderby={SapB1ReplicationConstants.Queue.EntityKey} asc" +
                           $"&$skip={skip}" +
                           $"&$top={top}" +
                           $"&$select={SapB1ReplicationConstants.Queue.EntityKey},{SapB1ReplicationConstants.CodeField}";

            UdoCollection<QueueRow>? result = await _client.GetAsync<UdoCollection<QueueRow>>(resource, ct);
            List<(string, string)> page = result?.Value.Select(r => (r.Code, r.EntityKey)).ToList() ?? new List<(string, string)>();

            if (page.Count == 0)
            {
                break;
            }

            collected.AddRange(page);
            skip += page.Count;

            if (page.Count < top)
            {
                break;
            }
        }

        return collected;
    }

    // ── Escritura ────────────────────────────────────────────────────────────

    public async Task EnqueueAsync(
        ReplicableEntityType entityType, string entityKey,
        ReplicationOperationType operationType, int maxRetryCount, CancellationToken ct = default)
    {
        // Idempotente: si ya existe una entrada activa no se duplica.
        var existing = await ExecuteAsync(() => FindQueueEntryAsync(entityType, entityKey, maxRetryCount, ct), entityType, entityKey, "buscar en cola", ct);
        if (existing is not null)
        {
            _logger.LogDebug("'{Key}' [{Type}] ya está en cola, se omite el encolado.", entityKey, entityType);
            return;
        }

        string table = ReplicableEntityTypeMapper.ToTableLiteral(entityType);
        string code = $"{table}-{entityKey}-{DateTime.Now:yyyyMMddHHmmss}";

        await ExecuteAsync(() => 
        _client.PostAsync<object>(SapB1ReplicationConstants.Queue.Endpoint, new Dictionary<string, object>
        {
            ["Code"] = code,
            ["Name"] = code,
            [SapB1ReplicationConstants.Queue.EntityType] = entityType.ToString(),
            [SapB1ReplicationConstants.Queue.EntityKey] = entityKey,
            [SapB1ReplicationConstants.Queue.Operation] = operationType == ReplicationOperationType.Insert
                               ? SapB1ReplicationConstants.Queue.OperationValues.Insert
                               : SapB1ReplicationConstants.Queue.OperationValues.Update,
            [SapB1ReplicationConstants.Queue.Status] = SapB1ReplicationConstants.Queue.StatusValues.Pending,
            [SapB1ReplicationConstants.Queue.RetryCount] = 0
        }, ct), entityType, entityKey, "encolar", ct);
    
        _logger.LogInformation(
            "Encolado '{Key}' [{Type}] – operación {Op}.", entityKey, entityType, operationType);
    }

    public async Task MarkAsReplicatedAsync(
        ReplicableEntityType entityType, string entityKey, int maxRetryCount, CancellationToken ct = default)
    {
        QueueRow? entry = await ExecuteAsync(() => FindQueueEntryAsync(entityType, entityKey, maxRetryCount, ct), entityType, entityKey, "buscar en cola", ct);
        if (entry is null)
        {
            _logger.LogWarning(
                "No se encontró '{Key}' [{Type}] en cola para marcar como {Action}.", entityKey, entityType, SapB1ReplicationConstants.Queue.StatusValues.Failed);
            return;
        }

        await ExecuteAsync(
            () => AppendAttemptAsync(entityType, entityKey, "OK", entry.Code, ct), entityType, entityKey, "registrar intento exitoso", ct);

        await ExecuteAsync(
            () => _client.PatchAsync(
                $"{SapB1ReplicationConstants.Queue.Endpoint}('{entry.Code}')",
                new Dictionary<string,object> { [SapB1ReplicationConstants.Queue.Status] = SapB1ReplicationConstants.Queue.StatusValues.Replicated }, ct),
            entityType, entityKey, "marcar como replicada", ct);

        _logger.LogInformation("'{Key}' [{Type}] marcado como {Action}.", entityKey, entityType, SapB1ReplicationConstants.Queue.StatusValues.Replicated);
    }

    public async Task MarkAsFailedAsync(
        ReplicableEntityType entityType, string entityKey, int maxRetryCount, string errorMessage, CancellationToken ct = default)
    {

        QueueRow? entry = await ExecuteAsync(() => FindQueueEntryAsync(entityType, entityKey, maxRetryCount, ct), entityType, entityKey, "buscar en cola", ct);
        if (entry is null)
        {
            _logger.LogWarning(
                "No se encontró '{Key}' [{Type}] en cola para marcar como {Action}.", entityKey, entityType, SapB1ReplicationConstants.Queue.StatusValues.Failed);
            return;
        }

        var newRetryCount = entry.RetryCount + 1;
        var safeMessage = errorMessage.Length > SapB1ReplicationConstants.Attempt.MessageMaxLength
            ? errorMessage[..SapB1ReplicationConstants.Attempt.MessageMaxLength]
            : errorMessage;

        await ExecuteAsync(
            () => AppendAttemptAsync(entityType, entityKey, safeMessage, entry.Code, ct), entityType, entityKey, "registrar intento fallido", ct);

        await ExecuteAsync(
       () => _client.PatchAsync(
           $"{SapB1ReplicationConstants.Queue.Endpoint}('{entry.Code}')",
           new Dictionary<string, object>
           {
               [SapB1ReplicationConstants.Queue.Status] = SapB1ReplicationConstants.Queue.StatusValues.Failed,
               [SapB1ReplicationConstants.Queue.RetryCount] = newRetryCount
           }, ct),
       entityType, entityKey, "marcar como fallida", ct);

        _logger.LogWarning(
            "'{Key}' [{Type}] marcado como {Action} (intento #{N}): {Err}.",
            entityKey, entityType, SapB1ReplicationConstants.Queue.StatusValues.Failed, newRetryCount, errorMessage);
    }

    // ── Helpers privados ─────────────────────────────────────────────────────
    private async Task<T> ExecuteAsync<T>(
    Func<Task<T>> operation,
    ReplicableEntityType entityType, string entityKey, string action,
    CancellationToken ct)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Error de transporte al {Action} de '{Key}' [{Type}] en SAP B1.", action, entityKey, entityType);

            throw new ReplicationControlStoreException(
                entityType, entityKey, $"Error de conexión al {action} de '{entityKey}' en SAP B1.", ex);
        }
    }

    // Overload sin valor de retorno, para las llamadas tipo PatchAsync/AppendAttemptAsync
    private Task ExecuteAsync(
        Func<Task> operation,
        ReplicableEntityType entityType, string entityKey, string action,
        CancellationToken ct)
        => ExecuteAsync(async () => { await operation(); return true; }, entityType, entityKey, action, ct);

    private async Task<QueueRow?> FindQueueEntryAsync(
    ReplicableEntityType entityType, string entityKey, int maxRetryCount,
    CancellationToken ct)
    {
        string entityTypeLiteral = ReplicableEntityTypeMapper.ToServiceLayerLiteral(entityType);

        var resource = $"{SapB1ReplicationConstants.Queue.Endpoint}" +
                       $"?$filter={SapB1ReplicationConstants.Queue.EntityType} eq '{entityTypeLiteral}'" +
                       $" and {SapB1ReplicationConstants.Queue.EntityKey} eq '{entityKey}'" +
                       $" and ({SapB1ReplicationConstants.Queue.Status} eq '{SapB1ReplicationConstants.Queue.StatusValues.Pending}'" +
                       $" or ({SapB1ReplicationConstants.Queue.Status} eq '{SapB1ReplicationConstants.Queue.StatusValues.Failed}' and {SapB1ReplicationConstants.Queue.RetryCount} lt 4))" +
                       $" and {SapB1ReplicationConstants.Queue.RetryCount} le {maxRetryCount}" +
                       "&$top=1";

        var result = await _client.GetAsync<UdoCollection<QueueRow>>(resource, ct);
        return result?.Value.FirstOrDefault();
    }

    private async Task AppendAttemptAsync(
        ReplicableEntityType entityType, string entityKey,
        string message, string codeQueue, CancellationToken ct)
    {
        if (await AttemptExistsAsync(codeQueue, message, ct))
        {
            _logger.LogDebug(
                "Intento duplicado omitido para '{Key}' [{Type}] – codeQueue '{CQ}', mensaje idéntico.",
                entityKey, entityType, codeQueue);
            return;
        }

        string table = ReplicableEntityTypeMapper.ToTableLiteral(entityType);
        string entityTypeLiteral = ReplicableEntityTypeMapper.ToServiceLayerLiteral(entityType);
        string code = $"{table}-{entityKey}-{DateTime.Now:yyyyMMddHHmmss}";

        await _client.PostAsync<object>(SapB1ReplicationConstants.Attempt.Endpoint, new Dictionary<string, object>
        {
            ["Code"] = code,
            ["Name"] = code,
            [SapB1ReplicationConstants.Attempt.EntityType] = entityTypeLiteral,
            [SapB1ReplicationConstants.Attempt.EntityKey] = entityKey,
            [SapB1ReplicationConstants.Attempt.Message] = message,
            [SapB1ReplicationConstants.Attempt.CreatedAt] = DateTime.Now.ToString("yyyy-MM-dd"),
            [SapB1ReplicationConstants.Attempt.CreatedTimeAt] = DateTime.Now.ToString("HHmm"),
            [SapB1ReplicationConstants.Attempt.CodeQueue] = codeQueue
        }, ct);
    }

    /// <summary>
    /// Verifica si ya existe un intento con el mismo <paramref name="codeQueue"/> y
    /// <paramref name="message"/>. Evita duplicar registros cuando el job reintenta
    /// una entidad que ya falló con el mismo error en la misma ejecución.
    /// </summary>
    private async Task<bool> AttemptExistsAsync(string codeQueue, string message, CancellationToken ct)
    {
        string safeCodeQueue = codeQueue.Replace("'", "''");
        string safeMessage   = message.Replace("'", "''")[..Math.Min(message.Length, SapB1ReplicationConstants.Attempt.MessageMaxLength)];

        var resource = $"{SapB1ReplicationConstants.Attempt.Endpoint}" +
                       $"?$filter={SapB1ReplicationConstants.Attempt.CodeQueue} eq '{safeCodeQueue}'" +
                       $" and contains({SapB1ReplicationConstants.Attempt.Message}, '{safeMessage}')" +
                       $"&$select={SapB1ReplicationConstants.CodeField}" +
                       "&$top=1";

        var result = await _client.GetAsync<UdoCollection<AttemptRow>>(resource, ct);
        return result?.Value.Count > 0;
    }

    // ── DTOs internos ────────────────────────────────────────────────────────

    private sealed class UdoCollection<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }

    private sealed class QueueRow
    {
        [JsonPropertyName("Code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName(SapB1ReplicationConstants.Queue.EntityKey)]
        public string EntityKey { get; set; } = string.Empty;

        [JsonPropertyName(SapB1ReplicationConstants.Queue.RetryCount)]
        public int RetryCount { get; set; }
    }

    private sealed class AttemptRow
    {
        [JsonPropertyName("Code")]
        public string Code { get; set; } = string.Empty;
    }
}