using System.ComponentModel.DataAnnotations;
using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Domain.Exceptions;

namespace GNA.AuroraIntegration.Application.Validation;

public sealed class ArticlePayloadValidator : IArticlePayloadValidator
{
    public void Validate(CreateAuroraArticleDto payload)
        => ValidatePayload(payload);

    public void Validate(UpdateAuroraArticleDto payload)
        => ValidatePayload(payload);

    private static void ValidatePayload(object payload)
    {
        ValidationContext context = new(payload);
        var validationResults = new List<ValidationResult>();

        bool isValid = Validator.TryValidateObject(payload, context, validationResults, validateAllProperties: true);
        if (isValid)
        {
            return;
        }

        string errorMessage = string.Join("; ", validationResults.Select(result => result.ErrorMessage));
        throw new UseCaseValidationException($"Payload inválido para replicación de artículos: {errorMessage}");
    }
}
