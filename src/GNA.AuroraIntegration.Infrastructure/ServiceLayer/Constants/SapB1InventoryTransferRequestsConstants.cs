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

        /// <summary>
        /// Campo estándar de documento (BoYesNoEnum) que indica si la Solicitud de Traslado
        /// fue cancelada mediante la acción oficial de Cancelar Documento en SAP B1. Verificar
        /// en Service Layer $metadata que el nombre/tipo coincide con la versión de B1 en uso.
        /// </summary>
        public const string CancelledField = "Cancelled";

        /// <summary>Valor literal que Service Layer usa para BoYesNoEnum = tYES.</summary>
        public const string CancelledYesValue = "tYES";

        public static class Lines
        {
            public const string LineNumField = "LineNum";
            public const string ItemCodeField = "ItemCode";
            public const string QuantityField = "Quantity";
        }
    }
}
