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

        public static class Lines
        {
            public const string LineNumField = "LineNum";
            public const string ItemCodeField = "ItemCode";
            public const string QuantityField = "Quantity";
        }
    }
}
