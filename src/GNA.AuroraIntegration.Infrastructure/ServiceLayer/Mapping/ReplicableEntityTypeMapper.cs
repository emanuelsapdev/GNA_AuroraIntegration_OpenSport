using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Enums.Schema;
using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Mapping
{
    internal class ReplicableEntityTypeMapper
    {
        public static string ToServiceLayerLiteral(ReplicableEntityType type) => type switch
        {
            ReplicableEntityType.Article => "ITEM",
            ReplicableEntityType.PurchaseOrder => "PURCHASE_ORDER",
            ReplicableEntityType.InventoryTransferRequest => "INVENTORY_TRANSFER_REQUEST",
            _ => throw new ArgumentOutOfRangeException(
                nameof(type), type, $"No existe mapeo de Service Layer para ReplicableEntityType '{type}'.")
        };

        public static string ToTableLiteral(ReplicableEntityType type) => type switch
        {
            ReplicableEntityType.Article => "OITM",
            ReplicableEntityType.PurchaseOrder => "OPOR",
            ReplicableEntityType.InventoryTransferRequest => "OWTQ",
            _ => throw new ArgumentOutOfRangeException(
                nameof(type), type, $"No existe mapeo de Service Layer para ReplicableEntityType '{type}'.")
        };
    }
}
