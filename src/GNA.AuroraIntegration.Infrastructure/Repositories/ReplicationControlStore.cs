using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;
using Microsoft.Extensions.Logging;
using RestSharp;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Infrastructure.Repositories;

/// <summary>
/// Implementa IReplicationControlStore sobre las UDTs de SAP B1 vía Service Layer.
///   @GNA_REP_QUEUE   – estado vivo de cada entidad pendiente.
///   @GNA_REP_ATTEMPT – histórico de intentos (OK y fallidos).
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

    public async Task<IReadOnlyList<string>> GetPendingKeysAsync(
        ReplicableEntityType entityType, int batchSize = 100, CancellationToken ct = default)
    {
        const int pageSize = 25;
        var collected = new List<string>(batchSize);
        int skip = 0;

        while (collected.Count < batchSize)
        {
            int remaining = batchSize - collected.Count;
            int top = Math.Min(pageSize, remaining);

            var resource = $"{SapB1ReplicationConstants.Queue.Endpoint}" +
                           $"?$filter={SapB1ReplicationConstants.Queue.EntityType} eq '{entityType}'" +
                           $" and {SapB1ReplicationConstants.Queue.Status} eq '{SapB1ReplicationConstants.Queue.StatusValues.Pending}'" +
                           $"&$orderby={SapB1ReplicationConstants.Queue.EntityKey} asc" +
                           $"&$skip={skip}" +
                           $"&$top={top}" +
                           $"&$select={SapB1ReplicationConstants.Queue.EntityKey}";

            UdoCollection<QueueRow>? result = await _client.GetAsync<UdoCollection<QueueRow>>(resource, ct);
            List<string> page = result?.Value.Select(r => r.EntityKey).ToList() ?? [];

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
        ReplicationOperationType operationType, int maxRetryCount, IEnumerable<string> excludedStatuses, CancellationToken ct = default)
    {
        // Idempotente: si ya existe una entrada activa no se duplica.
        var existing = await ExecuteAsync(() => FindQueueEntryAsync(entityType, entityKey, maxRetryCount, excludedStatuses, ct), entityType, entityKey, "buscar en cola", ct);
        if (existing is not null)
        {
            _logger.LogDebug("'{Key}' [{Type}] ya está en cola, se omite el encolado.", entityKey, entityType);
            return;
        }

        await ExecuteAsync(() =>
        _client.PostAsync<object>(SapB1ReplicationConstants.Queue.Endpoint, new
        {
            Code = NewCode(),
            Name = $"{entityType}/{entityKey}",
            U_EntityType = entityType.ToString(),
            U_EntityKey  = entityKey,
            U_Operation  = operationType == ReplicationOperationType.Insert
                               ? SapB1ReplicationConstants.Queue.OperationValues.Insert
                               : SapB1ReplicationConstants.Queue.OperationValues.Update,
            U_Status     = SapB1ReplicationConstants.Queue.StatusValues.Pending,
            U_RetryCount = 0
        }, ct), entityType, entityKey, "encolar", ct);

        _logger.LogInformation(
            "Encolado '{Key}' [{Type}] – operación {Op}.", entityKey, entityType, operationType);
    }

    public async Task MarkAsReplicatedAsync(
        ReplicableEntityType entityType, string entityKey, int maxRetryCount, IEnumerable<string> excludedStatuses, CancellationToken ct = default)
    {
        QueueRow? entry = await ExecuteAsync(() => FindQueueEntryAsync(entityType, entityKey, maxRetryCount, excludedStatuses, ct), entityType, entityKey, "buscar en cola", ct);
        if (entry is null)
        {
            _logger.LogWarning(
                "No se encontró '{Key}' [{Type}] en cola para marcar como {Action}.", entityKey, entityType, SapB1ReplicationConstants.Queue.StatusValues.Failed);
            return;
        }

        await ExecuteAsync(
            () => AppendAttemptAsync(entityType, entityKey, "OK", ct), entityType, entityKey, "registrar intento exitoso", ct);

        await ExecuteAsync(
            () => _client.PatchAsync(
                $"{SapB1ReplicationConstants.Queue.Endpoint}('{entry.Code}')",
                new { U_Status = SapB1ReplicationConstants.Queue.StatusValues.Replicated }, ct),
            entityType, entityKey, "marcar como replicada", ct);

        _logger.LogInformation("'{Key}' [{Type}] marcado como {Action}.", entityKey, entityType, SapB1ReplicationConstants.Queue.StatusValues.Replicated);
    }

    public async Task MarkAsFailedAsync(
        ReplicableEntityType entityType, string entityKey, int maxRetryCount, string errorMessage, IEnumerable<string> excludedStatuses, CancellationToken ct = default)
    {

        QueueRow? entry = await ExecuteAsync(() => FindQueueEntryAsync(entityType, entityKey, maxRetryCount, excludedStatuses, ct), entityType, entityKey, "buscar en cola", ct);
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
        () => AppendAttemptAsync(entityType, entityKey, safeMessage, ct), entityType, entityKey, "registrar intento fallido", ct);

        await ExecuteAsync(
       () => _client.PatchAsync(
           $"{SapB1ReplicationConstants.Queue.Endpoint}('{entry.Code}')",
           new
           {
               U_Status = SapB1ReplicationConstants.Queue.StatusValues.Failed,
               U_RetryCount = newRetryCount
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
    IEnumerable<string> excludedStatuses, CancellationToken ct)
    {
        var statusFilter = string.Join(" and ",
            excludedStatuses.Select(s => $"{SapB1ReplicationConstants.Queue.Status} ne '{s}'"));

        var resource = $"{SapB1ReplicationConstants.Queue.Endpoint}" +
                       $"?$filter={SapB1ReplicationConstants.Queue.EntityType} eq '{entityType}'" +
                       $" and {SapB1ReplicationConstants.Queue.EntityKey} eq '{entityKey}'" +
                       $" and ({statusFilter ?? "1 eq 1"})" +
                       $" and {SapB1ReplicationConstants.Queue.RetryCount} le {maxRetryCount}" +
                       "&$top=1";

        var result = await _client.GetAsync<UdoCollection<QueueRow>>(resource, ct);
        return result?.Value.FirstOrDefault();
    }

    private Task AppendAttemptAsync(
        ReplicableEntityType entityType, string entityKey,
        string message, CancellationToken ct)
        {
        var code = NewCode();
        return _client.PostAsync<object>(SapB1ReplicationConstants.Attempt.Endpoint, new
        {
            Code         = code,
            Name         = $"{entityType}/{entityKey}/{code}",
            U_EntityType = entityType.ToString(),
            U_EntityKey  = entityKey,
            U_Message    = message,
            U_CreatedAt  = DateTime.Now.ToString("yyyy-MM-dd")
        }, ct);
    }

    

    /// <summary>
    /// Genera un Code único de 8 caracteres alfanuméricos (límite de SAP B1 UDTs).
    /// Usa los primeros 8 chars de un Guid sin guiones.
    /// </summary>
    private static string NewCode() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

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

        [JsonPropertyName("U_EntityKey")]
        public string EntityKey { get; set; } = string.Empty;

        [JsonPropertyName("U_RetryCount")]
        public int RetryCount { get; set; }
    }
}