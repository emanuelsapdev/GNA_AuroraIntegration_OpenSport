using GNA.AuroraIntegration.Application.DTOs.Aurora;

namespace GNA.AuroraIntegration.Application.Validation;

public interface IPurchaseOrderPayloadValidator
{
    void Validate(CreateAuroraPurchaseOrderDto payload);
}
