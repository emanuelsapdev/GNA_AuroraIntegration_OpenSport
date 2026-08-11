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

public sealed class PurchaseOrderSyncUseCaseTests
{
    private readonly Mock<IPurchaseOrderReplicationRepository> _repositoryMock = new();
    private readonly Mock<IAuroraPurchaseOrderApiClient> _auroraApiMock = new();
    private readonly Mock<IPurchaseOrderPayloadValidator> _validatorMock = new();
    private readonly Mock<ILogger<PurchaseOrderSyncUseCase>> _loggerMock = new();

    private PurchaseOrderSyncUseCase CreateSut() // SUT: System Under Test
        => new(_repositoryMock.Object, _auroraApiMock.Object, _validatorMock.Object, _loggerMock.Object);

    [Fact(DisplayName = "⏺ Debe llamar a las dependencias con los parámetros esperados")]
    public async Task ExecuteAsync_ShouldCallDependencies_WithExpectedParameters()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        PurchaseOrder purchaseOrder = CreatePurchaseOrder(1001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(100, cancellationToken))
            .ReturnsAsync([purchaseOrder]);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("1001", null, cancellationToken))
            .ReturnsAsync((AuroraPurchaseOrderDto?)null);

        PurchaseOrderSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _auroraApiMock.Verify(c => c.GetPurchaseOrderByExternalIdAsync("1001", null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreatePurchaseOrderAsync(
            It.Is<CreateAuroraPurchaseOrderDto>(dto => dto.ExternalId == "1001" && dto.Articles.Length == 1),
            null,
            cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsReplicatedAsync("1001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Creación de OC nueva y reconciliación sin cambios de una OC ya existente")]
    public async Task ExecuteAsync_ShouldCompleteSuccessfully()
    {
        PurchaseOrder newOrder = CreatePurchaseOrder(2001, ("SKU-001", 10m));
        PurchaseOrder existingOrder = CreatePurchaseOrder(2002, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([newOrder, existingOrder]);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("2001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraPurchaseOrderDto?)null);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("2002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraPurchaseOrderDto { ExternalId = "2002", State = "PENDING" });

        // La OC existente ya tiene en Aurora exactamente la misma línea/cantidad que en SAP:
        // no debería dispararse ningún add/update/remove.
        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderArticlesAsync("2002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PurchaseOrderArticleStateDto { ArticleSku = "SKU-001", RequestedQuantity = 10, FulfilledQuantity = 0 }
            ]);

        PurchaseOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(2, result.processed);
        Assert.Equal(2, result.successful);
        Assert.Equal(0, result.failed);
        _auroraApiMock.Verify(c => c.CreatePurchaseOrderAsync(It.IsAny<CreateAuroraPurchaseOrderDto>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _auroraApiMock.Verify(c => c.AddPurchaseOrderArticlesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PurchaseOrderArticleDto>>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.UpdatePurchaseOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PurchaseOrderArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.RemovePurchaseOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "⏺ Debe reconciliar líneas de una OC modificada: agrega, edita y elimina")]
    public async Task ExecuteAsync_ShouldReconcileLines_WhenPurchaseOrderIsModified()
    {
        PurchaseOrder purchaseOrder = CreatePurchaseOrder(
            5001,
            ("SKU-001", 10m),   // sin cambios
            ("SKU-002", 5m),    // cantidad cambió (Aurora tenía 3)
            ("SKU-003", 7m));   // línea nueva, no existe en Aurora

        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([purchaseOrder]);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("5001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraPurchaseOrderDto { ExternalId = "5001", State = "PENDING" });

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderArticlesAsync("5001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PurchaseOrderArticleStateDto { ArticleSku = "SKU-001", RequestedQuantity = 10, FulfilledQuantity = 0 },
                new PurchaseOrderArticleStateDto { ArticleSku = "SKU-002", RequestedQuantity = 3, FulfilledQuantity = 0 },
                new PurchaseOrderArticleStateDto { ArticleSku = "SKU-OLD", RequestedQuantity = 2, FulfilledQuantity = 0 } // ya no está en SAP
            ]);

        PurchaseOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.AddPurchaseOrderArticlesAsync(
            "5001",
            It.Is<IReadOnlyList<PurchaseOrderArticleDto>>(list => list.Count == 1 && list[0].ArticleSku == "SKU-003" && list[0].Quantity == 7),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        _auroraApiMock.Verify(c => c.UpdatePurchaseOrderArticleAsync(
            "5001", "SKU-002",
            It.Is<PurchaseOrderArticleDto>(dto => dto.Quantity == 5),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        _auroraApiMock.Verify(c => c.RemovePurchaseOrderArticleAsync("5001", "SKU-OLD", null, It.IsAny<CancellationToken>()), Times.Once);

        // SKU-001 no cambió: no debe tocarse.
        _auroraApiMock.Verify(c => c.UpdatePurchaseOrderArticleAsync("5001", "SKU-001", It.IsAny<PurchaseOrderArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);

        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsReplicatedAsync("5001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ No debe editar ni eliminar líneas ya cumplidas (fulfilledQuantity > 0) en Aurora")]
    public async Task ExecuteAsync_ShouldSkipLineChanges_WhenAlreadyFulfilledInAurora()
    {
        // SAP quiere cambiar SKU-010 de cantidad, pero Aurora ya recibió unidades de esa línea.
        PurchaseOrder purchaseOrder = CreatePurchaseOrder(6001, ("SKU-010", 99m));

        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([purchaseOrder]);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("6001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraPurchaseOrderDto { ExternalId = "6001", State = "CHECKING" });

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderArticlesAsync("6001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PurchaseOrderArticleStateDto { ArticleSku = "SKU-010", RequestedQuantity = 5, FulfilledQuantity = 2 },
                new PurchaseOrderArticleStateDto { ArticleSku = "SKU-999", RequestedQuantity = 1, FulfilledQuantity = 1 } // ya no está en SAP, pero cumplida
            ]);

        PurchaseOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.UpdatePurchaseOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PurchaseOrderArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.RemovePurchaseOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.AddPurchaseOrderArticlesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<PurchaseOrderArticleDto>>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // Se marca como replicada igual: la OC en sí se procesó correctamente, solo se
        // omitieron cambios puntuales por seguridad operativa.
        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsReplicatedAsync("6001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe manejar PurchaseOrderAuroraApiException")]
    public async Task ExecuteAsync_ShouldHandlePurchaseOrderAuroraApiException()
    {
        PurchaseOrder purchaseOrder = CreatePurchaseOrder(3001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([purchaseOrder]);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("3001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraPurchaseOrderDto?)null);

        _auroraApiMock
            .Setup(c => c.CreatePurchaseOrderAsync(It.IsAny<CreateAuroraPurchaseOrderDto>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PurchaseOrderAuroraApiException("3001", "Simulated API failure"));

        PurchaseOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(0, result.successful);
        Assert.Equal(1, result.failed);
        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsFailedAsync("3001", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe pasar el CancellationToken")]
    public async Task ExecuteAsync_ShouldPassCancellationToken()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        PurchaseOrder purchaseOrder = CreatePurchaseOrder(4001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(It.IsAny<int>(), cancellationToken))
            .ReturnsAsync([purchaseOrder]);

        _auroraApiMock
            .Setup(c => c.GetPurchaseOrderByExternalIdAsync("4001", null, cancellationToken))
            .ReturnsAsync((AuroraPurchaseOrderDto?)null);

        _auroraApiMock
            .Setup(c => c.CreatePurchaseOrderAsync(It.IsAny<CreateAuroraPurchaseOrderDto>(), null, cancellationToken))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.MarkPurchaseOrderAsReplicatedAsync("4001", cancellationToken))
            .Returns(Task.CompletedTask);

        PurchaseOrderSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _repositoryMock.Verify(r => r.GetPendingPurchaseOrdersAsync(100, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.GetPurchaseOrderByExternalIdAsync("4001", null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreatePurchaseOrderAsync(It.IsAny<CreateAuroraPurchaseOrderDto>(), null, cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsReplicatedAsync("4001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe propagar la excepción cuando el servicio falla")]
    public async Task ExecuteAsync_WhenServiceFails_ShouldPropagate()
    {
        _repositoryMock
            .Setup(r => r.GetPendingPurchaseOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable"));

        PurchaseOrderSyncUseCase useCase = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync());

        _repositoryMock.Verify(r => r.MarkPurchaseOrderAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PurchaseOrder CreatePurchaseOrder(int docEntry, params (string Sku, decimal Quantity)[] lines) => new()
    {
        DocEntry = docEntry,
        DocNum = docEntry,
        Lines = [.. lines.Select((line, index) => new PurchaseOrderLine
        {
            LineOrder = index,
            ArticleSku = line.Sku,
            Quantity = line.Quantity
        })]
    };
}
