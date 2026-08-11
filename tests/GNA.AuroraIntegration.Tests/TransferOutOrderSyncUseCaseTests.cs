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

public sealed class TransferOutOrderSyncUseCaseTests
{
    private readonly Mock<ITransferOutOrderReplicationRepository> _repositoryMock = new();
    private readonly Mock<IAuroraTransferOutOrderApiClient> _auroraApiMock = new();
    private readonly Mock<ITransferOutOrderPayloadValidator> _validatorMock = new();
    private readonly Mock<ILogger<TransferOutOrderSyncUseCase>> _loggerMock = new();

    private TransferOutOrderSyncUseCase CreateSut() // SUT: System Under Test
        => new(_repositoryMock.Object, _auroraApiMock.Object, _validatorMock.Object, _loggerMock.Object);

    [Fact(DisplayName = "⏺ Debe llamar a las dependencias con los parámetros esperados")]
    public async Task ExecuteAsync_ShouldCallDependencies_WithExpectedParameters()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(1001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(100, cancellationToken))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("1001", null, cancellationToken))
            .ReturnsAsync((AuroraTransferOutOrderDto?)null);

        TransferOutOrderSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _auroraApiMock.Verify(c => c.GetTransferOutOrderByExternalIdAsync("1001", null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreateTransferOutOrderAsync(
            It.Is<CreateAuroraTransferOutOrderDto>(dto => dto.ExternalId == "1001" && dto.Articles.Length == 1),
            null,
            cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync("1001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Creación de orden nueva y reconciliación sin cambios de una orden ya existente")]
    public async Task ExecuteAsync_ShouldCompleteSuccessfully()
    {
        TransferOutOrder newOrder = CreateTransferOutOrder(2001, ("SKU-001", 10m));
        TransferOutOrder existingOrder = CreateTransferOutOrder(2002, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([newOrder, existingOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("2001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraTransferOutOrderDto?)null);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("2002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraTransferOutOrderDto { ExternalId = "2002", State = "PENDING" });

        // La orden existente ya tiene en Aurora exactamente la misma línea/cantidad que en SAP:
        // no debería dispararse ningún update/remove.
        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderArticlesAsync("2002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TransferOutOrderArticleStateDto { ArticleSku = "SKU-001", RequestedQuantity = 10, FulfilledQuantity = 0 }
            ]);

        TransferOutOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(2, result.processed);
        Assert.Equal(2, result.successful);
        Assert.Equal(0, result.failed);
        _auroraApiMock.Verify(c => c.CreateTransferOutOrderAsync(It.IsAny<CreateAuroraTransferOutOrderDto>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _auroraApiMock.Verify(c => c.UpdateTransferOutOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferOutOrderArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.RemoveTransferOutOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "⏺ Debe reconciliar líneas de una orden modificada: edita y elimina, pero NO agrega (la API no lo permite)")]
    public async Task ExecuteAsync_ShouldReconcileLines_WhenTransferOutOrderIsModified()
    {
        // A diferencia de PurchaseOrderSyncUseCase, SKU-003 (línea nueva en SAP, ausente en
        // Aurora) NO debe generar ninguna llamada a Aurora: la API de transfer-out-orders no
        // expone alta de artículos sobre una orden existente. Solo se espera log de advertencia.
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(
            5001,
            ("SKU-001", 10m),   // sin cambios
            ("SKU-002", 5m),    // cantidad cambió (Aurora tenía 3)
            ("SKU-003", 7m));   // línea nueva, no existe en Aurora — no se puede agregar vía API

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("5001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraTransferOutOrderDto { ExternalId = "5001", State = "PENDING" });

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderArticlesAsync("5001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TransferOutOrderArticleStateDto { ArticleSku = "SKU-001", RequestedQuantity = 10, FulfilledQuantity = 0 },
                new TransferOutOrderArticleStateDto { ArticleSku = "SKU-002", RequestedQuantity = 3, FulfilledQuantity = 0 },
                new TransferOutOrderArticleStateDto { ArticleSku = "SKU-OLD", RequestedQuantity = 2, FulfilledQuantity = 0 } // ya no está en SAP
            ]);

        TransferOutOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.UpdateTransferOutOrderArticleAsync(
            "5001", "SKU-002",
            It.Is<TransferOutOrderArticleDto>(dto => dto.Quantity == 5),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        _auroraApiMock.Verify(c => c.RemoveTransferOutOrderArticleAsync("5001", "SKU-OLD", null, It.IsAny<CancellationToken>()), Times.Once);

        // SKU-001 no cambió: no debe tocarse.
        _auroraApiMock.Verify(c => c.UpdateTransferOutOrderArticleAsync("5001", "SKU-001", It.IsAny<TransferOutOrderArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // SKU-003 es nueva en SAP: no existe ningún método de alta de artículos que se pueda
        // haber llamado por error (verificado implícitamente por la ausencia del método en
        // IAuroraTransferOutOrderApiClient); la orden igual se marca como replicada.
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync("5001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ No debe editar ni eliminar líneas ya cumplidas (fulfilledQuantity > 0) en Aurora")]
    public async Task ExecuteAsync_ShouldSkipLineChanges_WhenAlreadyFulfilledInAurora()
    {
        // SAP quiere cambiar SKU-010 de cantidad, pero Aurora ya recibió unidades de esa línea.
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(6001, ("SKU-010", 99m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("6001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraTransferOutOrderDto { ExternalId = "6001", State = "CHECKING" });

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderArticlesAsync("6001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TransferOutOrderArticleStateDto { ArticleSku = "SKU-010", RequestedQuantity = 5, FulfilledQuantity = 2 },
                new TransferOutOrderArticleStateDto { ArticleSku = "SKU-999", RequestedQuantity = 1, FulfilledQuantity = 1 } // ya no está en SAP, pero cumplida
            ]);

        TransferOutOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.UpdateTransferOutOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferOutOrderArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.RemoveTransferOutOrderArticleAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // Se marca como replicada igual: la orden en sí se procesó correctamente, solo se
        // omitieron cambios puntuales por seguridad operativa.
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync("6001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe cancelar en Aurora una orden cancelada en SAP que ya existía allí")]
    public async Task ExecuteAsync_ShouldCancelInAurora_WhenTransferOutOrderIsCancelledAndExists()
    {
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(7001, cancelled: true, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("7001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraTransferOutOrderDto { ExternalId = "7001", State = "PENDING" });

        TransferOutOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.CancelTransferOutOrderAsync("7001", null, It.IsAny<CancellationToken>()), Times.Once);
        _auroraApiMock.Verify(c => c.CreateTransferOutOrderAsync(It.IsAny<CreateAuroraTransferOutOrderDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.GetTransferOutOrderArticlesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync("7001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ No debe llamar a Aurora para cancelar una orden que nunca existió allí")]
    public async Task ExecuteAsync_ShouldNotCallCancel_WhenCancelledTransferOutOrderNeverExistedInAurora()
    {
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(7002, cancelled: true, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("7002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraTransferOutOrderDto?)null);

        TransferOutOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.CancelTransferOutOrderAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.CreateTransferOutOrderAsync(It.IsAny<CreateAuroraTransferOutOrderDto>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // La orden nunca existió en Aurora: se trata como no-op exitoso, no como error.
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync("7002", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe manejar TransferOutOrderAuroraApiException")]
    public async Task ExecuteAsync_ShouldHandleTransferOutOrderAuroraApiException()
    {
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(3001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("3001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraTransferOutOrderDto?)null);

        _auroraApiMock
            .Setup(c => c.CreateTransferOutOrderAsync(It.IsAny<CreateAuroraTransferOutOrderDto>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransferOutOrderAuroraApiException("3001", "Simulated API failure"));

        TransferOutOrderSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(0, result.successful);
        Assert.Equal(1, result.failed);
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsFailedAsync("3001", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe pasar el CancellationToken")]
    public async Task ExecuteAsync_ShouldPassCancellationToken()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        TransferOutOrder transferOutOrder = CreateTransferOutOrder(4001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), cancellationToken))
            .ReturnsAsync([transferOutOrder]);

        _auroraApiMock
            .Setup(c => c.GetTransferOutOrderByExternalIdAsync("4001", null, cancellationToken))
            .ReturnsAsync((AuroraTransferOutOrderDto?)null);

        _auroraApiMock
            .Setup(c => c.CreateTransferOutOrderAsync(It.IsAny<CreateAuroraTransferOutOrderDto>(), null, cancellationToken))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.MarkTransferOutOrderAsReplicatedAsync("4001", cancellationToken))
            .Returns(Task.CompletedTask);

        TransferOutOrderSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _repositoryMock.Verify(r => r.GetPendingTransferOutOrdersAsync(100, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.GetTransferOutOrderByExternalIdAsync("4001", null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreateTransferOutOrderAsync(It.IsAny<CreateAuroraTransferOutOrderDto>(), null, cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync("4001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe propagar la excepción cuando el servicio falla")]
    public async Task ExecuteAsync_WhenServiceFails_ShouldPropagate()
    {
        _repositoryMock
            .Setup(r => r.GetPendingTransferOutOrdersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable"));

        TransferOutOrderSyncUseCase useCase = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync());

        _repositoryMock.Verify(r => r.MarkTransferOutOrderAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TransferOutOrder CreateTransferOutOrder(
        int docEntry, params (string Sku, decimal Quantity)[] lines)
        => CreateTransferOutOrder(docEntry, cancelled: false, lines);

    private static TransferOutOrder CreateTransferOutOrder(
        int docEntry, bool cancelled, params (string Sku, decimal Quantity)[] lines) => new()
    {
        DocEntry = docEntry,
        DocNum = docEntry,
        Cancelled = cancelled,
        Lines = [.. lines.Select((line, index) => new TransferOutOrderLine
        {
            LineOrder = index,
            ArticleSku = line.Sku,
            Quantity = line.Quantity
        })]
    };
}
