namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

/// <summary>
/// Constantes SAP B1 Service Layer para la UDT de Banners (@GNA_AUR_BANNERS).
///
/// Esta UDT es la tabla vinculada ("Linked Table") del UDF U_GNA_AUR_Banner en OITM
/// (ver ReplicationSchemaConstants.ItemsTable.Fields.Banner / SapB1ItemsConstants.Items.BannerField).
/// Por ser un LinkedTable, OITM únicamente persiste el Code (máx. 8 chars) del banner
/// seleccionado — nunca su Name. Mismo caso que ProductBrand (ver SapB1ProductBrandsConstants):
/// Service Layer no expone esta relación como navigation property OData, así que resolver el
/// Name requiere una consulta adicional explícita contra este recurso.
///
/// Reglas de nomenclatura en Service Layer (igual que toda UDT, ver SapB1ReplicationConstants):
///   - Endpoint de una UDT  →  U_{TableName}   (sin '@')
///   - Clave primaria       →  Code (string, máx. 8 chars alfanuméricos)
///   - Descripción estándar →  Name
/// </summary>
internal static class SapB1BannersConstants
{
    public static class Banners
    {
        /// <summary>Endpoint Service Layer: /U_GNA_AUR_BANNERS</summary>
        public const string Endpoint = "U_GNA_AUR_BANNERS";

        public const string CodeField = "Code";
        public const string NameField = "Name";
    }
}
