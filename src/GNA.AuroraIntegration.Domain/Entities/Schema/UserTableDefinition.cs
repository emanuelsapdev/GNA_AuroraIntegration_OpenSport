using GNA.AuroraIntegration.Domain.Enums.Schema;

namespace GNA.AuroraIntegration.Domain.Entities.Schema;

/// <summary>
/// Definición de una tabla de usuario (UDT) a provisionar en SAP B1.
/// </summary>
public sealed class UserTableDefinition
{
    /// <summary>Nombre sin el prefijo "@" (Service Layer lo agrega/gestiona internamente).</summary>
    public string TableName { get; }
    public string TableDescription { get; }
    public UserTableType TableType { get; }

    public UserTableDefinition(string tableName, string tableDescription, UserTableType tableType)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("TableName no puede ser vacío.", nameof(tableName));

        TableName = tableName;
        TableDescription = tableDescription;
        TableType = tableType;
    }
}