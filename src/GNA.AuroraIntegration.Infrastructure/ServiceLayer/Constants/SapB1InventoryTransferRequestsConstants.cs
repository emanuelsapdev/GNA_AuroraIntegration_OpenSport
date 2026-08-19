namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

/// <summary>
/// Constantes SAP B1 Service Layer para Solicitudes de Traslado (OWTQ/WTQ1 — "Inventory
/// Transfer Request" en la nomenclatura del SDK/Service Layer, objeto DI API
/// oInventoryTransferRequest).
///
/// ⚠️ El nombre del recurso REST ("InventoryTransferRequests") y los nombres de campo de
/// DocumentLines (LineNum/ItemCode/Quantity) se asumen análogos a PurchaseOrders/POR1 por ser
/// el patrón estándar de todo documento de marketing en SAP B1 — no se verificó explícitamente
/// contra $metadata de Service Layer en este ambiente. Confirmar antes de producción (ver
/// PROJECT_PROGRESS.md).
/// </summary>
public static class SapB1InventoryTransferRequestsConstants
{
    public static class InventoryTransferRequests
    {
        /// <summary>Recurso Service Layer para Solicitudes de Traslado (OWTQ/WTQ1).</summary>
        public const string Endpoint = "InventoryTransferRequests";

        public const string DocEntryField = "DocEntry";
        public const string DocNumField = "DocNum";
        public const string DocumentLinesField = "DocumentLines";
        public const string DocumentStatusField = "DocumentStatus";
        public const string TypeClosureField = "U_GNA_AUR_TypeClosure";

        public static class Lines
        {
            public const string LineNumField = "LineNum";
            public const string ItemCodeField = "ItemCode";
            public const string QuantityField = "Quantity";
        }

        /// <summary>Valor literal que Service Layer usa para BoYesNoEnum = tYES.</summary>
        public const string CancelledYesValue = "tYES";

        /// <summary>Valor literal que Service Layer usa para TypeClosure = MANUAL</summary>
        public const string TypeClosureManualValue = "MANUAL";

        /// <summary>Valor literal que Service Layer usa para DocumentStatus = bost_Close</summary>
        public const string DocumentStatusCloseValue = "bost_Close";
    }
}
