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
        public const string DocumentStatusField = "DocumentStatus";
        public const string CancelledField = "Cancelled";
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
