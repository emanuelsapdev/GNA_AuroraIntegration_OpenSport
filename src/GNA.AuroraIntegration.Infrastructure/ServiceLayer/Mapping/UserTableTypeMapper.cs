// Infrastructure/ServiceLayer/Mapping/UserTableTypeMapper.cs
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Enums.Schema;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;

/// <summary>
/// Traduce UserTableType (dominio) al literal string que espera
/// SAP B1 Service Layer en el recurso UserTablesMD.
/// </summary>
internal static class UserTableTypeMapper
{
    public static string ToServiceLayerLiteral(UserTableType type) => type switch
    {
        UserTableType.NoObject => "bott_NoObject",
        UserTableType.MasterData => "bott_MasterData",
        UserTableType.MasterDataLines => "bott_MasterDataLines",
        UserTableType.Document => "bott_Document",
        UserTableType.DocumentLines => "bott_DocumentLines",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, $"No existe mapeo de Service Layer para UserTableType '{type}'.")
    };
}