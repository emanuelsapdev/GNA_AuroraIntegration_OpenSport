using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants
{
    public sealed class SapB1ItemsConstants
    {
        public sealed class Items
        {
            public const string Endpoint = "Items";

            public const string ItemCodeField = "ItemCode";
            public const string ItemNameField = "ItemName";
            public const string BarCodeField = "BarCode";

            // ⚠️ Estos 3 campos apuntaban antes a nombres que EnsureReplicationSchemaUseCase
            // nunca provisiona (U_GNA_AUR_BannerID/BrandID/CategoryName) — corregidos para
            // coincidir con los UDFs reales (ver ReplicationSchemaConstants.ItemsTable.Fields).
            // Los 3 son LinkedTable (Banner → GNA_AUR_BANNERS, ProductBrand → GNA_AUR_MARCAS,
            // LogisticsCategory → GNA_AUR_CATLOG), así que el valor guardado en OITM es el Code
            // de esa tabla, no el Name — ver GetNamesByCodeAsync en ArticleServiceLayerLookupRepository.
            public const string BannerField = "U_GNA_AUR_Banner";
            public const string ProductBrandField = "U_GNA_AUR_Marca";
            public const string LogisticsCategoryField = "U_GNA_AUR_CatLog";
            public const string IsBulkyField = "U_GNA_AUR_IsBulky";
            public const string IsCagedField = "U_GNA_AUR_IsCaged";
        }
    }
}
