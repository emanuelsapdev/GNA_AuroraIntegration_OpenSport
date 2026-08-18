using GNA.AuroraIntegration.Application.DTOs.Aurora.InventoryTransferRequest;

namespace GNA.AuroraIntegration.Application.Validation;

public interface IInventoryTransferRequestPayloadValidator
{
    void Validate(CreateAuroraInventoryTransferRequestDto payload);
}
