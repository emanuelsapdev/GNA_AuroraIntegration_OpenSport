// Infrastructure/ServiceLayer/Mapping/UserFieldTypeMapper.cs
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Enums.Schema;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;

/// <summary>
/// Traduce UserFieldType (dominio) al literal string que espera
/// SAP B1 Service Layer en el recurso UserFieldsMD (propiedad "Type").
/// </summary>
internal static class UserFieldTypeMapper
{
    public static string ToServiceLayerLiteral(UserFieldType type) => type switch
    {
        UserFieldType.Alpha => "db_Alpha",
        UserFieldType.Numeric => "db_Numeric",
        UserFieldType.Date => "db_Date",
        UserFieldType.Float => "db_Float",
        UserFieldType.Memo => "db_Memo",
        UserFieldType.Time => "db_Time",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, $"No existe mapeo de Service Layer para UserFieldType '{type}'.")
    };
}