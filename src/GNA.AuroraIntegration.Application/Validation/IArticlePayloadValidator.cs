using GNA.AuroraIntegration.Application.DTOs.Aurora.Article;

namespace GNA.AuroraIntegration.Application.Validation;

public interface IArticlePayloadValidator
{
    void Validate(CreateAuroraArticleDto payload);
    void Validate(UpdateAuroraArticleDto payload);
}
