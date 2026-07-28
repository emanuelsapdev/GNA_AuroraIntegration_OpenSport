using GNA.AuroraIntegration.Domain.Enums.Schema;
using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping;

/// <summary>
/// Traduce UserObjectType (dominio) al literal string que espera
/// SAP B1 Service Layer en el recurso UserObjectsMD (propiedad "ObjectType").
/// </summary>
internal static class UserObjectTypeMapper
{
    public static string ToServiceLayerLiteral(UserObjectType type) => type switch
    {
        UserObjectType.MasterData => "boud_MasterData",
        UserObjectType.Document => "boud_Document",
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, $"No existe mapeo de Service Layer para UserObjectType '{type}'.")
    };
}