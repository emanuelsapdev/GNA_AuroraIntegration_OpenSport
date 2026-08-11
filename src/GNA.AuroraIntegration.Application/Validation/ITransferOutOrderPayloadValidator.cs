using GNA.AuroraIntegration.Application.DTOs.Aurora;

namespace GNA.AuroraIntegration.Application.Validation;

public interface ITransferOutOrderPayloadValidator
{
    void Validate(CreateAuroraTransferOutOrderDto payload);
}
