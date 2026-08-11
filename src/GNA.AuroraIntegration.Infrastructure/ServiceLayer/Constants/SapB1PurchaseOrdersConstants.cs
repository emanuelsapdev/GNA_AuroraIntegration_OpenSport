namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

public static class SapB1PurchaseOrdersConstants
{
    public static class PurchaseOrders
    {
        /// <summary>Recurso Service Layer para Órdenes de Compra (OPOR/POR1).</summary>
        public const string Endpoint = "PurchaseOrders";

        public const string DocEntryField = "DocEntry";
        public const string DocNumField = "DocNum";
        public const string DocumentLinesField = "DocumentLines";

        /// <summary>
        /// Campo estándar de documento (BoYesNoEnum) que indica si la OC fue cancelada
        /// mediante la acción oficial de Cancelar Documento en SAP B1. Verificar en Service
        /// Layer $metadata que el nombre/tipo coincide con la versión de B1 en uso.
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
