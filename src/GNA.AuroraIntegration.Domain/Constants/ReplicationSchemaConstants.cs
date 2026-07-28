using static GNA.AuroraIntegration.Domain.Entities.Schema.UserObjectDefinition;

namespace GNA.AuroraIntegration.Domain.Constants;



/// <summary>
/// Nombres canónicos de las UDTs y sus campos lógicos para el esquema de replicación.
/// Libres de prefijos tecnológicos (sin '@', sin 'U_'): representan el contrato de esquema
/// del dominio, independientemente del motor de persistencia.
/// Consumidos por la capa Application al provisionar el esquema vía ISchemaProvisioningService.
/// </summary>
public static class ReplicationSchemaConstants
{
    private const string sufix = "";


    /// <summary>Tabla de cola: estado vivo de cada entidad pendiente de replicar.</summary>
    public static class QueueTable
    {
        /// <summary>Nombre de la tabla sin prefijo '@' (requerido por UserTablesMD en SL).</summary>
        public const string Name = "GNA_REP_QUEUE" + sufix;

        /// <summary>Nombre físico en SAP B1 HANA/SQL Server (con prefijo '@').</summary>
        public const string DbName = "@GNA_REP_QUEUE" + sufix;

        public const string Description = "Cola de replicación a Aurora";

        /// <summary>Nombres lógicos de los campos (sin prefijo 'U_').</summary>
        public static class Fields
        {
            public const string EntityType = "EntityType";
            public const string EntityKey  = "EntityKey";
            public const string Operation  = "Operation";
            public const string Status     = "Status";
            public const string RetryCount = "RetryCount";
        }
    }

    /// <summary>Tabla de intentos: histórico inmutable de cada intento de replicación.</summary>
    public static class AttemptTable
    {
        public const string Name = "GNA_REP_ATTEMPT" + sufix;

        public const string DbName = "@GNA_REP_ATTEMPT" + sufix;

        public const string Description = "Intentos de replicación";

        public static class Fields
        {
            public const string EntityType = "EntityType";
            public const string EntityKey  = "EntityKey";
            public const string Message    = "Message";
            public const string CreatedAt  = "CreatedAt";
        }
    }

    public static class LogisticsCategoryTable
    {
        public const string Name = "GN_CATLOG" + sufix;
        public const string DbName = "@GN_CATLOG" + sufix;
        public const string Description = "Categorías Logísticas";
    }

    public static class LogisticsCategoryUserObject
    {
        public const string Code = "CatLog" + sufix;
        public const string Name = "Categorías Logísticas" + sufix;
        public const string MenuCaption = "Categorías Logísticas" + sufix;
        public const int FatherMenuID = 11520; 
        public const int Position = 14;
        public const string MenuUID = "CatLog" + sufix;
        public const string TableName = "GN_CATLOG" + sufix;

    }

    public static class ProductBrandsTable
    {
        public const string Name = "GN_MARCAS" + sufix;
        public const string DbName = "@GN_MARCAS" + sufix;
        public const string Description = "Marcas de Productos";

    }

    public static class ProductBrandsUserObject
    {
        public const string Code = "Marcas" + sufix;
        public const string Name = "Marcas" + sufix;
        public const string MenuCaption = "Marcas" + sufix;
        public const int FatherMenuID = 11520;
        public const int Position = 15;
        public const string MenuUID = "Marcas" + sufix;
        public const string TableName = "GN_MARCAS" + sufix;

    }

    public static class ItemsTable
    {
        public const string Name = "OITM";

        public const string DbName = "OITM";

        public static class Fields
        {
            public const string LogisticsCategory = "GN_CatLog" + sufix;
            public const string ProductBrand = "GN_Marca" + sufix;
        }
    }
}
