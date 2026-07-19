namespace GNA.AuroraIntegration.Domain.Exceptions;

/// <summary>
/// Error al crear o verificar una tabla/campo de usuario en SAP B1 vía Service Layer.
/// </summary>
public sealed class SchemaProvisioningException : AuroraIntegrationException
{
    public string ObjectName { get; }

    public SchemaProvisioningException(string objectName, string message)
        : base(message)
    {
        ObjectName = objectName;
    }

    public SchemaProvisioningException(string objectName, string message, Exception inner)
        : base(message, inner)
    {
        ObjectName = objectName;
    }
}