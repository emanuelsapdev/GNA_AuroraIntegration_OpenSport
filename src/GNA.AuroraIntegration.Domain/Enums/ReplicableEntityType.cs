namespace GNA.AuroraIntegration.Domain.Enums;

/// <summary>
/// Tipos de entidad replicables hacia Aurora. Agregar un valor acá es el único
/// cambio de código necesario para soportar una entidad nueva a pedido del cliente
/// — no requiere tocar el esquema físico de @GNA_REP_CFG/@GNA_REP_LOG.
/// </summary>
public enum ReplicableEntityType
{
    Article = 0,
    PurchaseOrder = 1,
    SalesOrder = 2,
    ReturnOrder = 3,
    TransferInOrder = 4,
    TransferOutOrder = 5,
    Inventory = 6
}