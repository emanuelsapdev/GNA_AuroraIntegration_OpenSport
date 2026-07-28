using GNA.AuroraIntegration.Domain.Enums;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;

/// <summary>
/// Traduce UserFieldSubType (dominio) al literal string que espera
/// SAP B1 Service Layer en el recurso UserFieldsMD (propiedad "SubType").
/// </summary>
internal static class SapYesNoMapper
{
    public static string ToServiceLayerLiteral(bool value) => value switch
    {
        true => "tYES",
        _ => "tNO"
    };
}
       