// Domain/Entities/Schema/UserFieldDefinition.cs
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Enums.Schema;
using System.ComponentModel;

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
    public string? LinkedTable { get; }
    public List<ValidValueDefinition>? ValidValues { get; }
    public string? DefaultValue { get; }

    public UserFieldDefinition(
        string name,
        string description,
        UserFieldType type,
        UserFieldSubType subType = UserFieldSubType.None,
        int? size = null,
        string? linkedTable = null,
        List<ValidValueDefinition>? validValues = null, 
        string? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name no puede ser vacío.", nameof(name));

        Name = name;
        Description = description;
        Type = type;
        SubType = subType;
        Size = size;
        LinkedTable = linkedTable;
        ValidValues = validValues;
        DefaultValue = defaultValue;
    }

    public sealed class ValidValueDefinition
    {
        public string Value { get; }
        public string Description { get; }
        public ValidValueDefinition(string value, string description)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value no puede ser vacío.", nameof(value));
            Value = value;
            Description = description;
        }
    }
}