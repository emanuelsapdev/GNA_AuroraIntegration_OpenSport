using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace GNA.AuroraIntegration.Tests;

public sealed class ArticleSyncUseCaseTests
{
    private readonly Mock<IArticleReplicationRepository> _repositoryMock = new();
    private readonly Mock<IAuroraArticleApiClient> _auroraApiMock = new();
    private readonly Mock<IArticlePayloadValidator> _validatorMock = new();
    private readonly Mock<ILogger<ArticleSyncUseCase>> _loggerMock = new();

    private ArticleSyncUseCase CreateSut() // SUT: System Under Test
        => new(_repositoryMock.Object, _auroraApiMock.Object, _validatorMock.Object, _loggerMock.Object);

    [Fact(DisplayName = "⏺ Debe llamar a las dependencias con los parámetros esperados")]
    public async Task ExecuteAsync_ShouldCallDependencies_WithExpectedParameters()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Article article = CreateArticle(It.IsAny<string>());

        _repositoryMock
            .Setup(r => r.GetPendingArticlesAsync(100, cancellationToken))
            .ReturnsAsync([article]);

        _auroraApiMock
            .Setup(c => c.GetArticleBySkuAsync(It.Is<string>(sku => sku == article.Sku), warehouse: null, cancellationToken))
            .ReturnsAsync((AuroraArticleDto?)null);

        ArticleSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _auroraApiMock.Verify(c => c.GetArticleBySkuAsync(It.Is<string>(sku => sku == article.Sku), warehouse: null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreateArticleAsync(
            It.Is<CreateAuroraArticleDto>(dto => dto.Sku == article.Sku && dto.Ean == article.PrimaryEan),
            warehouse: null,
            cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync(article.Sku, cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Creación y actualización de artículos")]
    public async Task ExecuteAsync_ShouldCompleteSuccessfully()
    {
        Article createArticle = CreateArticle("A001");
        Article updateArticle = CreateArticle("A002");

        _repositoryMock
            .Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([createArticle, updateArticle]);

        _auroraApiMock
            .Setup(c => c.GetArticleBySkuAsync("A001", warehouse: null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraArticleDto?)null);

        _auroraApiMock
            .Setup(c => c.GetArticleBySkuAsync("A002", warehouse: null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraArticleDto { Sku = "A002", Name = "Artículo 2" });

        ArticleSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(2, result.processed);
        Assert.Equal(2, result.successful);
        Assert.Equal(0, result.failed);
        _auroraApiMock.Verify(c => c.CreateArticleAsync(It.IsAny<CreateAuroraArticleDto>(), warehouse: null, It.IsAny<CancellationToken>()), Times.Once);
        _auroraApiMock.Verify(c => c.UpdateArticleAsync("A002", It.IsAny<UpdateAuroraArticleDto>(), warehouse: null, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "⏺ Debe manejar ArticleAuroraApiException")]
    public async Task ExecuteAsync_ShouldHandleArticleAuroraApiException()
    {
        Article article = CreateArticle("A001");

        _repositoryMock
            .Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([article]);

        _auroraApiMock
            .Setup(c => c.GetArticleBySkuAsync("A001", warehouse: null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraArticleDto?)null);

        _auroraApiMock
            .Setup(c => c.CreateArticleAsync(It.IsAny<CreateAuroraArticleDto>(), warehouse: null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArticleAuroraApiException("A001", "Simulated API failure"));

        ArticleSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(0, result.successful);
        Assert.Equal(1, result.failed);
        _repositoryMock.Verify(r => r.MarkArticleAsFailedAsync("A001", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe pasar el CancellationToken")]
    public async Task ExecuteAsync_ShouldPassCancellationToken()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        Article article = CreateArticle("A001");

        _repositoryMock
            .Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), cancellationToken))
            .ReturnsAsync([article]);

        _auroraApiMock
            .Setup(c => c.GetArticleBySkuAsync("A001", warehouse: null, cancellationToken))
            .ReturnsAsync(new AuroraArticleDto { Sku = "A001" });

        _auroraApiMock
            .Setup(c => c.UpdateArticleAsync("A001", It.IsAny<UpdateAuroraArticleDto>(), warehouse: null, cancellationToken))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.MarkArticleAsReplicatedAsync("A001", cancellationToken))
            .Returns(Task.CompletedTask);

        ArticleSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _repositoryMock.Verify(r => r.GetPendingArticlesAsync(100, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.GetArticleBySkuAsync("A001", warehouse: null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.UpdateArticleAsync("A001", It.IsAny<UpdateAuroraArticleDto>(), warehouse: null, cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync("A001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe propagar la excepción cuando el servicio falla")]
    public async Task ExecuteAsync_WhenServiceFails_ShouldPropagate()
    {
        _repositoryMock
            .Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable"));

        ArticleSyncUseCase useCase = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync());

        _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Article CreateArticle(string sku) => new()
    {
        Sku = sku,
        Name = $"Artículo {sku}",
        PrimaryEan = "1234567890123",
        AdditionalEans = [],
        WeightInGr = 100,
        HeightInCm = 10,
        WidthInCm = 5,
        LengthInCm = 20,
        IsConsumable = false,
        HasProductionBatch = false,
        HasDueDate = false,
        HasSerialNumber = false
    };
}
