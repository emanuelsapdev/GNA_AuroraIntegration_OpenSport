using System.ComponentModel.DataAnnotations;
using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Domain.Exceptions;

namespace GNA.AuroraIntegration.Application.Validation;

public sealed class TransferOutOrderPayloadValidator : ITransferOutOrderPayloadValidator
{
    public void Validate(CreateAuroraTransferOutOrderDto payload)
    {
        ValidationContext context = new(payload);
        var validationResults = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(payload, context, validationResults, validateAllProperties: true);
        if (isValid)
        {
            return;
        }

        string errorMessage = string.Join("; ", validationResults.Select(result => result.ErrorMessage));
        throw new UseCaseValidationException($"Payload inválido para replicación de órdenes de transferencia de salida: {errorMessage}");
    }
}
