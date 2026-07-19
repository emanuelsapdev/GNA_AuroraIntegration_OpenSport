namespace GNA.AuroraIntegration.Domain.Enums;

    /// <summary>
    /// Estado de replicación de un artículo hacia Aurora WMS.
    /// </summary>
    public enum ReplicationStatus
    {
        Pending = 0,
        Processing = 1,
        Success = 2,
        Error = 3,
        Discarded = 4
    }

