using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.DTOs.Aurora.InventoryTransferRequest;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound;
using GNA.AuroraIntegration.Application.Validation;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace GNA.AuroraIntegration.Tests;

public sealed class InventoryTransferRequestSyncUseCaseTests
{
    private readonly Mock<IInventoryTransferRequestReplicationRepository> _repositoryMock = new();
    private readonly Mock<IAuroraInventoryTransferRequestApiClient> _auroraApiMock = new();
    private readonly Mock<IInventoryTransferRequestPayloadValidator> _validatorMock = new();
    private readonly Mock<ILogger<InventoryTransferRequestSyncUseCase>> _loggerMock = new();

    private InventoryTransferRequestSyncUseCase CreateSut() // SUT: System Under Test
        => new(_repositoryMock.Object, _auroraApiMock.Object, _validatorMock.Object, _loggerMock.Object);

    [Fact(DisplayName = "⏺ Debe llamar a las dependencias con los parámetros esperados")]
    public async Task ExecuteAsync_ShouldCallDependencies_WithExpectedParameters()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(1001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(100, cancellationToken))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("1001", null, cancellationToken))
            .ReturnsAsync((AuroraInventoryTransferRequestDto?)null);

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _auroraApiMock.Verify(c => c.GetInventoryTransferRequestByExternalIdAsync("1001", null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreateInventoryTransferRequestAsync(
            It.Is<CreateAuroraInventoryTransferRequestDto>(dto => dto.ExternalId == "1001" && dto.Articles.Length == 1),
            null,
            cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync("1001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Creación de orden nueva y reconciliación sin cambios de una orden ya existente")]
    public async Task ExecuteAsync_ShouldCompleteSuccessfully()
    {
        InventoryTransferRequest newOrder = CreateInventoryTransferRequest(2001, ("SKU-001", 10m));
        InventoryTransferRequest existingOrder = CreateInventoryTransferRequest(2002, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([newOrder, existingOrder]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("2001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraInventoryTransferRequestDto?)null);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("2002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraInventoryTransferRequestDto { ExternalId = "2002", State = "PENDING" });

        // La orden existente ya tiene en Aurora exactamente la misma línea/cantidad que en SAP:
        // no debería dispararse ningún update/remove.
        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestArticlesAsync("2002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventoryTransferRequestArticleStateDto { ArticleSku = "SKU-001", RequestedQuantity = 10, FulfilledQuantity = 0 }
            ]);

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(2, result.processed);
        Assert.Equal(2, result.successful);
        Assert.Equal(0, result.failed);
        _auroraApiMock.Verify(c => c.CreateInventoryTransferRequestAsync(It.IsAny<CreateAuroraInventoryTransferRequestDto>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _auroraApiMock.Verify(c => c.UpdateInventoryTransferRequestArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InventoryTransferRequestArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.RemoveInventoryTransferRequestArticleAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact(DisplayName = "⏺ Debe reconciliar líneas de una orden modificada: edita y elimina, pero NO agrega (la API no lo permite)")]
    public async Task ExecuteAsync_ShouldReconcileLines_WhenInventoryTransferRequestIsModified()
    {
        // A diferencia de PurchaseOrderSyncUseCase, SKU-003 (línea nueva en SAP, ausente en
        // Aurora) NO debe generar ninguna llamada a Aurora: la API de transfer-out-orders no
        // expone alta de artículos sobre una orden existente. Solo se espera log de advertencia.
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(
            5001,
            ("SKU-001", 10m),   // sin cambios
            ("SKU-002", 5m),    // cantidad cambió (Aurora tenía 3)
            ("SKU-003", 7m));   // línea nueva, no existe en Aurora — no se puede agregar vía API

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("5001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraInventoryTransferRequestDto { ExternalId = "5001", State = "PENDING" });

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestArticlesAsync("5001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventoryTransferRequestArticleStateDto { ArticleSku = "SKU-001", RequestedQuantity = 10, FulfilledQuantity = 0 },
                new InventoryTransferRequestArticleStateDto { ArticleSku = "SKU-002", RequestedQuantity = 3, FulfilledQuantity = 0 },
                new InventoryTransferRequestArticleStateDto { ArticleSku = "SKU-OLD", RequestedQuantity = 2, FulfilledQuantity = 0 } // ya no está en SAP
            ]);

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.UpdateInventoryTransferRequestArticleAsync(
            "5001", "SKU-002",
            It.Is<InventoryTransferRequestArticleDto>(dto => dto.Quantity == 5),
            null,
            It.IsAny<CancellationToken>()), Times.Once);

        _auroraApiMock.Verify(c => c.RemoveInventoryTransferRequestArticleAsync("5001", "SKU-OLD", null, It.IsAny<CancellationToken>()), Times.Once);

        // SKU-001 no cambió: no debe tocarse.
        _auroraApiMock.Verify(c => c.UpdateInventoryTransferRequestArticleAsync("5001", "SKU-001", It.IsAny<InventoryTransferRequestArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // SKU-003 es nueva en SAP: no existe ningún método de alta de artículos que se pueda
        // haber llamado por error (verificado implícitamente por la ausencia del método en
        // IAuroraInventoryTransferRequestApiClient); la orden igual se marca como replicada.
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync("5001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ No debe editar ni eliminar líneas ya cumplidas (fulfilledQuantity > 0) en Aurora")]
    public async Task ExecuteAsync_ShouldSkipLineChanges_WhenAlreadyFulfilledInAurora()
    {
        // SAP quiere cambiar SKU-010 de cantidad, pero Aurora ya recibió unidades de esa línea.
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(6001, ("SKU-010", 99m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("6001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraInventoryTransferRequestDto { ExternalId = "6001", State = "CHECKING" });

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestArticlesAsync("6001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventoryTransferRequestArticleStateDto { ArticleSku = "SKU-010", RequestedQuantity = 5, FulfilledQuantity = 2 },
                new InventoryTransferRequestArticleStateDto { ArticleSku = "SKU-999", RequestedQuantity = 1, FulfilledQuantity = 1 } // ya no está en SAP, pero cumplida
            ]);

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.UpdateInventoryTransferRequestArticleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InventoryTransferRequestArticleDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.RemoveInventoryTransferRequestArticleAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // Se marca como replicada igual: la orden en sí se procesó correctamente, solo se
        // omitieron cambios puntuales por seguridad operativa.
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync("6001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe cancelar en Aurora una orden cancelada en SAP que ya existía allí")]
    public async Task ExecuteAsync_ShouldCancelInAurora_WhenInventoryTransferRequestIsCancelledAndExists()
    {
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(7001, isClosedManual: true, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("7001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuroraInventoryTransferRequestDto { ExternalId = "7001", State = "PENDING" });

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.CancelInventoryTransferRequestAsync("7001", null, It.IsAny<CancellationToken>()), Times.Once);
        _auroraApiMock.Verify(c => c.CreateInventoryTransferRequestAsync(It.IsAny<CreateAuroraInventoryTransferRequestDto>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.GetInventoryTransferRequestArticlesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync("7001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ No debe llamar a Aurora para cancelar una orden que nunca existió allí")]
    public async Task ExecuteAsync_ShouldNotCallCancel_WhenCancelledInventoryTransferRequestNeverExistedInAurora()
    {
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(7002, isClosedManual: true, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("7002", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraInventoryTransferRequestDto?)null);

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.successful);
        Assert.Equal(0, result.failed);

        _auroraApiMock.Verify(c => c.CancelInventoryTransferRequestAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _auroraApiMock.Verify(c => c.CreateInventoryTransferRequestAsync(It.IsAny<CreateAuroraInventoryTransferRequestDto>(), null, It.IsAny<CancellationToken>()), Times.Never);

        // La orden nunca existió en Aurora: se trata como no-op exitoso, no como error.
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync("7002", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe manejar InventoryTransferRequestAuroraApiException")]
    public async Task ExecuteAsync_ShouldHandleInventoryTransferRequestAuroraApiException()
    {
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(3001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("3001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuroraInventoryTransferRequestDto?)null);

        _auroraApiMock
            .Setup(c => c.CreateInventoryTransferRequestAsync(It.IsAny<CreateAuroraInventoryTransferRequestDto>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryTransferRequestAuroraApiException("3001", "Simulated API failure"));

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        (int processed, int successful, int failed) result = await useCase.ExecuteAsync();

        Assert.Equal(1, result.processed);
        Assert.Equal(0, result.successful);
        Assert.Equal(1, result.failed);
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsFailedAsync("3001", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe pasar el CancellationToken")]
    public async Task ExecuteAsync_ShouldPassCancellationToken()
    {
        CancellationToken cancellationToken = new CancellationTokenSource().Token;
        InventoryTransferRequest InventoryTransferRequest = CreateInventoryTransferRequest(4001, ("SKU-001", 10m));

        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), cancellationToken))
            .ReturnsAsync([InventoryTransferRequest]);

        _auroraApiMock
            .Setup(c => c.GetInventoryTransferRequestByExternalIdAsync("4001", null, cancellationToken))
            .ReturnsAsync((AuroraInventoryTransferRequestDto?)null);

        _auroraApiMock
            .Setup(c => c.CreateInventoryTransferRequestAsync(It.IsAny<CreateAuroraInventoryTransferRequestDto>(), null, cancellationToken))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.MarkInventoryTransferRequestAsReplicatedAsync("4001", cancellationToken))
            .Returns(Task.CompletedTask);

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        await useCase.ExecuteAsync(cancellationToken);

        _repositoryMock.Verify(r => r.GetPendingInventoryTransferRequestAsync(100, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.GetInventoryTransferRequestByExternalIdAsync("4001", null, cancellationToken), Times.Once);
        _auroraApiMock.Verify(c => c.CreateInventoryTransferRequestAsync(It.IsAny<CreateAuroraInventoryTransferRequestDto>(), null, cancellationToken), Times.Once);
        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync("4001", cancellationToken), Times.Once);
    }

    [Fact(DisplayName = "⏺ Debe propagar la excepción cuando el servicio falla")]
    public async Task ExecuteAsync_WhenServiceFails_ShouldPropagate()
    {
        _repositoryMock
            .Setup(r => r.GetPendingInventoryTransferRequestAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable"));

        InventoryTransferRequestSyncUseCase useCase = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync());

        _repositoryMock.Verify(r => r.MarkInventoryTransferRequestAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static InventoryTransferRequest CreateInventoryTransferRequest(
        int docEntry, params (string Sku, decimal Quantity)[] lines)
        => CreateInventoryTransferRequest(docEntry, isClosedManual: false, lines);

    private static InventoryTransferRequest CreateInventoryTransferRequest(
        int docEntry, bool isClosedManual, params (string Sku, decimal Quantity)[] lines) => new()
    {
        DocEntry = docEntry,
        DocNum = docEntry,
        IsClosedManual = isClosedManual,
        Lines = [.. lines.Select((line, index) => new InventoryTransferRequestLine
        {
            LineOrder = index,
            ArticleSku = line.Sku,
            Quantity = line.Quantity
        })]
    };
}
