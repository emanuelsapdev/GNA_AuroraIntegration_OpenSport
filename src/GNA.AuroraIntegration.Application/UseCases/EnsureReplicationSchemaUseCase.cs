// Application/UseCases/EnsureReplicationSchemaUseCase.cs
using GNA.AuroraIntegration.Domain.Constants;
using GNA.AuroraIntegration.Domain.Entities.Schema;
using GNA.AuroraIntegration.Domain.Enums.Schema;
using GNA.AuroraIntegration.Domain.Interfaces;
using static GNA.AuroraIntegration.Domain.Constants.ReplicationSchemaConstants;

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
        await EnsureLogisticsCategoryTableAsync(ct);
        await EnsureProductBrandsTableAsync(ct);
        await EnsureArticlesFieldsAsync(ct);
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

    private async Task EnsureLogisticsCategoryTableAsync(CancellationToken ct)
    {
            var logisticsCategoryTable = new UserTableDefinition(
            ReplicationSchemaConstants.LogisticsCategoryTable.Name,
            ReplicationSchemaConstants.LogisticsCategoryTable.Description,
            UserTableType.MasterData);
        await _schema.EnsureUserTableAsync(logisticsCategoryTable, ct);

        var logisticsCategoryUserObject = new UserObjectDefinition(
            code: ReplicationSchemaConstants.LogisticsCategoryUserObject.Code,
            name: ReplicationSchemaConstants.LogisticsCategoryUserObject.Name,
            tableName: ReplicationSchemaConstants.LogisticsCategoryUserObject.TableName,
            canClose: true,
            canFind: true,
            menuItem: true,
            menuCaption: ReplicationSchemaConstants.LogisticsCategoryUserObject.MenuCaption,
            fatherMenuID: ReplicationSchemaConstants.LogisticsCategoryUserObject.FatherMenuID,
            position: ReplicationSchemaConstants.LogisticsCategoryUserObject.Position,
            menuUID: ReplicationSchemaConstants.LogisticsCategoryUserObject.MenuUID,
            canCreateDefaultForm: true,
            objectType: UserObjectType.MasterData);
        await _schema.EnsureUserObjectAsync(logisticsCategoryUserObject, ct);
    }

    private async Task EnsureProductBrandsTableAsync(CancellationToken ct)
    {
        var productBrandsTable = new UserTableDefinition(
            ReplicationSchemaConstants.ProductBrandsTable.Name,
            ReplicationSchemaConstants.ProductBrandsTable.Description,
            UserTableType.MasterData);
        await _schema.EnsureUserTableAsync(productBrandsTable, ct);

        var productBrandsUserObject = new UserObjectDefinition(
            code: ReplicationSchemaConstants.ProductBrandsUserObject.Code,
            name: ReplicationSchemaConstants.ProductBrandsUserObject.Name,
            tableName: ReplicationSchemaConstants.ProductBrandsUserObject.TableName,
            canClose: true,
            canFind: true,
            menuItem: true,
            menuCaption: ReplicationSchemaConstants.ProductBrandsUserObject.MenuCaption,
            fatherMenuID: ReplicationSchemaConstants.ProductBrandsUserObject.FatherMenuID,
            position: ReplicationSchemaConstants.ProductBrandsUserObject.Position,
            menuUID: ReplicationSchemaConstants.ProductBrandsUserObject.MenuUID,
            canCreateDefaultForm: true,
            objectType: UserObjectType.MasterData);
        await _schema.EnsureUserObjectAsync(productBrandsUserObject, ct);

    }

    private async Task EnsureArticlesFieldsAsync(CancellationToken ct)
    {
        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.ItemsTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.ItemsTable.Fields.LogisticsCategory, "Categoría Logística", UserFieldType.Alpha, size: 30, linkedTable: ReplicationSchemaConstants.LogisticsCategoryTable.Name), ct);

        await _schema.EnsureUserFieldAsync(ReplicationSchemaConstants.ItemsTable.DbName,
            new UserFieldDefinition(ReplicationSchemaConstants.ItemsTable.Fields.ProductBrand, "Marca de Producto", UserFieldType.Alpha, size: 30, linkedTable: ReplicationSchemaConstants.ProductBrandsTable.Name), ct);

    }
}