// Infrastructure/ServiceLayer/Mapping/UserFieldSubTypeMapper.cs
using GNA.AuroraIntegration.Domain.Enums;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;

/// <summary>
/// Traduce UserFieldSubType (dominio) al literal string que espera
/// SAP B1 Service Layer en el recurso UserFieldsMD (propiedad "SubType").
/// </summary>
internal static class UserFieldSubTypeMapper
{
    public static string ToServiceLayerLiteral(UserFieldSubType subType) => subType switch
    {
        UserFieldSubType.None => "st_None",
        UserFieldSubType.Phone => "st_Phone",
        UserFieldSubType.Percentage => "st_Percentage",
        UserFieldSubType.Password => "st_Password",
        UserFieldSubType.Sum => "st_Sum",
        UserFieldSubType.Rate => "st_Rate",
        UserFieldSubType.Link => "st_Link",
        UserFieldSubType.Time => "st_Time",
        UserFieldSubType.Address => "st_Address",
        _ => throw new ArgumentOutOfRangeException(
            nameof(subType), subType, $"No existe mapeo de Service Layer para UserFieldSubType '{subType}'.")
    };
}