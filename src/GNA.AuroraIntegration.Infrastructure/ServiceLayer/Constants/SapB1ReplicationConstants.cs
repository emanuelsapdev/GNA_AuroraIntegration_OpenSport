namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.Constants;

/// <summary>
/// Constantes SAP B1 Service Layer para los recursos UDT del esquema de replicación.
///
/// Reglas de nomenclatura en Service Layer:
///   - Endpoint de una UDT NoObject  →  U_{TableName}   (sin '@')
///   - Campos de usuario              →  U_{FieldName}   (prefijo obligatorio)
///   - Clave primaria de UDT          →  Code (string, máx. 8 chars alfanuméricos)
///
/// Estas constantes son internas a Infrastructure; ninguna capa superior las conoce.
/// </summary>
internal static class SapB1ReplicationConstants
{
    // ── Campos estándar de toda UDT ──────────────────────────────────────────

    /// <summary>Clave primaria de UDTs en SAP B1 (máx. 8 chars alfanuméricos).</summary>
    public const string CodeField = "Code";

    /// <summary>Campo descripción estándar de UDTs.</summary>
    public const string NameField = "Name";

    // ── @GNA_REP_QUEUE ───────────────────────────────────────────────────────

    /// <summary>Constantes para la UDT de cola de replicación.</summary>
    public static class Queue
    {
        /// <summary>Endpoint Service Layer: /U_GNA_REP_QUEUE</summary>
        public const string Endpoint = "U_GNA_REP_QUEUE";

        // Campos (prefijo U_ obligatorio en Service Layer)
        public const string EntityType = "U_EntityType";
        public const string EntityKey  = "U_EntityKey";
        public const string Operation  = "U_Operation";
        public const string Status     = "U_Status";
        public const string RetryCount = "U_RetryCount";

        /// <summary>Valores posibles del campo U_Status.</summary>
        public static class StatusValues
        {
            public const string Pending    = "PENDING";
            public const string Replicated = "REPLICATED";
            public const string Failed     = "FAILED";
        }

        /// <summary>Valores posibles del campo U_Operation.</summary>
        public static class OperationValues
        {
            public const string Insert = "I";
            public const string Update = "U";
        }
    }

    // ── @GNA_REP_ATTEMPT ─────────────────────────────────────────────────────

    /// <summary>Constantes para la UDT de histórico de intentos.</summary>
    public static class Attempt
    {
        /// <summary>Endpoint Service Layer: /U_GNA_REP_ATTEMPT</summary>
        public const string Endpoint = "U_GNA_REP_ATTEMPT";

        public const string EntityType = "U_EntityType";
        public const string EntityKey  = "U_EntityKey";
        public const string Message    = "U_Message";
        public const string CreatedAt  = "U_CreatedAt";

        public const int MessageMaxLength = 254;
    }
}
