// Application/UseCases/EnsureReplicationSchemaUseCase.cs
using GNA.AuroraIntegration.Domain.Constants;
using GNA.AuroraIntegration.Domain.Entities.Schema;
using GNA.AuroraIntegration.Domain.Enums.Schema;
using GNA.AuroraIntegration.Domain.Interfaces;

namespace GNA.AuroraIntegration.Application.UseCases;

/// <summary>
/// Garantiza al arranque que existan las tablas compartidas de replicación:
/// @GNA_REP_QUEUE (estado vivo de cada entidad pendiente) y
/// @GNA_REP_ATTEMPT (histórico de intentos), usadas por todas las entidades
/// replicables mediante el discriminador EntityType.
/// </summary>
public sealed class EnsureReplicationSchemaUseCase : IEnsureReplicationSchemaUseCase
{
    private readonly ISchemaProvisioningService _schema;

    public EnsureReplicationSchemaUseCase(ISchemaProvisioningService schema)
    {
        _schema = schema;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        await EnsureQueueTableAsync(ct);
        await EnsureAttemptTableAsync(ct);
    }

    private async Task EnsureQueueTableAsync(CancellationToken ct)
    {
        var queueTable = new UserTableDefinition(
            ReplicationSchemaConstants.QueueTable.Name,
            ReplicationSchemaConstants.QueueTable.Description,
            UserTableType.NoObject);
        await _schema.EnsureUserTableAsync(queueTable, ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.QueueTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.QueueTable.Fields.EntityType, "Tipo de entidad", UserFieldType.Alpha, size: 30), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.QueueTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.QueueTable.Fields.EntityKey, "Clave de la entidad", UserFieldType.Alpha, size: 50), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.QueueTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.QueueTable.Fields.Operation, "Alta o modificación", UserFieldType.Alpha, size: 1), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.QueueTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.QueueTable.Fields.Status, "Estado de replicación", UserFieldType.Alpha, size: 20), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.QueueTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.QueueTable.Fields.RetryCount, "Reintentos realizados", UserFieldType.Numeric), ct);
    }

    private async Task EnsureAttemptTableAsync(CancellationToken ct)
    {
        var attemptTable = new UserTableDefinition(
            ReplicationSchemaConstants.AttemptTable.Name,
            ReplicationSchemaConstants.AttemptTable.Description,
            UserTableType.NoObject);
        await _schema.EnsureUserTableAsync(attemptTable, ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.AttemptTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.AttemptTable.Fields.EntityType, "Tipo de entidad", UserFieldType.Alpha, size: 30), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.AttemptTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.AttemptTable.Fields.EntityKey, "Clave de la entidad", UserFieldType.Alpha, size: 50), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.AttemptTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.AttemptTable.Fields.Message, "Resultado del intento", UserFieldType.Memo), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.AttemptTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.AttemptTable.Fields.CreatedAt, "Fecha del intento", UserFieldType.Date), ct);
    }
}