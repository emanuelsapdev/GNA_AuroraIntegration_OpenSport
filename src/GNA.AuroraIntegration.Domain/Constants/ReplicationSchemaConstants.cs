namespace GNA.AuroraIntegration.Domain.Constants;

/// <summary>
/// Nombres canónicos de las UDTs y sus campos lógicos para el esquema de replicación.
/// Libres de prefijos tecnológicos (sin '@', sin 'U_'): representan el contrato de esquema
/// del dominio, independientemente del motor de persistencia.
/// Consumidos por la capa Application al provisionar el esquema vía ISchemaProvisioningService.
/// </summary>
public static class ReplicationSchemaConstants
{
    /// <summary>Tabla de cola: estado vivo de cada entidad pendiente de replicar.</summary>
    public static class QueueTable
    {
        /// <summary>Nombre de la tabla sin prefijo '@' (requerido por UserTablesMD en SL).</summary>
        public const string Name = "GNA_REP_QUEUE";

        /// <summary>Nombre físico en SAP B1 HANA/SQL Server (con prefijo '@').</summary>
        public const string DbName = "@GNA_REP_QUEUE";

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
        public const string Name = "GNA_REP_ATTEMPT";

        public const string DbName = "@GNA_REP_ATTEMPT";

        public const string Description = "Intentos de replicación";

        public static class Fields
        {
            public const string EntityType = "EntityType";
            public const string EntityKey  = "EntityKey";
            public const string Message    = "Message";
            public const string CreatedAt  = "CreatedAt";
        }
    }
}
