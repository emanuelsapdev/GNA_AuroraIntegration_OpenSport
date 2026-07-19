using GNA.AuroraIntegration.Domain.Entities.Schema;

namespace GNA.AuroraIntegration.Domain.Interfaces;

/// <summary>
/// Garantiza la existencia de tablas y campos de usuario en SAP B1 vía Service Layer.
/// Idempotente: si el objeto ya existe, no falla ni lo duplica.
/// </summary>
public interface ISchemaProvisioningService
{
    /// <summary>Crea la tabla si no existe. No falla si ya existe.</summary>
    Task EnsureUserTableAsync(UserTableDefinition table, CancellationToken ct = default);

    /// <summary>Crea el campo sobre la tabla indicada si no existe. No falla si ya existe.</summary>
    Task EnsureUserFieldAsync(string tableName, UserFieldDefinition field, CancellationToken ct = default);
}