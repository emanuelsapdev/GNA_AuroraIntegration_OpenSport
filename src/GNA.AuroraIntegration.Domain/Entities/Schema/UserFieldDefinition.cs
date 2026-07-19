// Domain/Entities/Schema/UserFieldDefinition.cs
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Enums.Schema;

namespace GNA.AuroraIntegration.Domain.Entities.Schema;

/// <summary>
/// Definición de un campo de usuario (UDF) a provisionar sobre una UDT existente.
/// </summary>
public sealed class UserFieldDefinition
{
    public string Name { get; }
    public string Description { get; }
    public UserFieldType Type { get; }
    public int? Size { get; }
    public UserFieldSubType SubType { get; }

    public UserFieldDefinition(
        string name,
        string description,
        UserFieldType type,
        UserFieldSubType subType = UserFieldSubType.None,
        int? size = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name no puede ser vacío.", nameof(name));

        Name = name;
        Description = description;
        Type = type;
        SubType = subType;
        Size = size;
    }
}