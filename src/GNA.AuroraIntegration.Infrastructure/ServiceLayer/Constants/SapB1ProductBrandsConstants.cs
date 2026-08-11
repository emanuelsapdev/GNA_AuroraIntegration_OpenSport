namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

/// <summary>
/// Constantes SAP B1 Service Layer para la UDT de Marcas de Productos (@GNA_AUR_MARCAS).
///
/// Esta UDT es la tabla vinculada ("Linked Table") del UDF U_GNA_AUR_Marca en OITM
/// (ver ReplicationSchemaConstants.ItemsTable.Fields.ProductBrand / SapB1ItemsConstants.Items.ProductBrandField).
/// Por ser un LinkedTable, OITM únicamente persiste el Code (máx. 8 chars) de la marca
/// seleccionada — nunca su Name. Service Layer no expone esta relación como navigation
/// property OData (no hay $expand posible para UDFs LinkedTable), así que resolver el
/// Name requiere una consulta adicional explícita contra este recurso.
///
/// Reglas de nomenclatura en Service Layer (igual que toda UDT, ver SapB1ReplicationConstants):
///   - Endpoint de una UDT  →  U_{TableName}   (sin '@')
///   - Clave primaria       →  Code (string, máx. 8 chars alfanuméricos)
///   - Descripción estándar →  Name
/// </summary>
internal static class SapB1ProductBrandsConstants
{
    public static class ProductBrands
    {
        /// <summary>Endpoint Service Layer: /U_GNA_AUR_MARCAS</summary>
        public const string Endpoint = "U_GNA_AUR_MARCAS";

        public const string CodeField = "Code";
        public const string NameField = "Name";
    }
}
