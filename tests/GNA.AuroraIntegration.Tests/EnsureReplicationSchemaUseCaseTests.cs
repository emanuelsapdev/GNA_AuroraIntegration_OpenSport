using Moq;
using Xunit;
using GNA.AuroraIntegration.Application.UseCases;
using GNA.AuroraIntegration.Domain.Constants;
using GNA.AuroraIntegration.Domain.Entities.Schema;
using GNA.AuroraIntegration.Domain.Enums.Schema;
using GNA.AuroraIntegration.Domain.Interfaces;
using Xunit.Abstractions;

namespace GNA.AuroraIntegration.Tests;

/// <summary>
/// Tests para validar la creación de las tablas y campos de replicación.
/// </summary>
public class EnsureReplicationSchemaUseCaseTests
{
    private readonly Mock<ISchemaProvisioningService> _mockSchemaService;
    private readonly EnsureReplicationSchemaUseCase _useCase;
    private readonly ITestOutputHelper _output;

    public EnsureReplicationSchemaUseCaseTests(ITestOutputHelper output)
    {
        _mockSchemaService = new Mock<ISchemaProvisioningService>();
        _useCase = new EnsureReplicationSchemaUseCase(_mockSchemaService.Object);
        _output = output;
    }

    [Fact(DisplayName = "⏺ Debe crear la tabla GNA_REP_QUEUE con los parametros esperados.")]
    public async Task ExecuteAsync_ShouldCreateQueueTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GNA_REP_QUEUE");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserTableAsync(
                It.Is<UserTableDefinition>(t =>
                    t.TableName == ReplicationSchemaConstants.QueueTable.Name &&
                    t.TableDescription == ReplicationSchemaConstants.QueueTable.Description &&
                    t.TableType == UserTableType.NoObject),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Tabla GNA_REP_QUEUE creada correctamente");
    }

    [Fact(DisplayName = "⏺ Debe crear la tabla GNA_REP_ATTEMPT con los parametros esperados.")]
    public async Task ExecuteAsync_ShouldCreateAttemptTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GNA_REP_ATTEMPT");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserTableAsync(
                It.Is<UserTableDefinition>(t =>
                    t.TableName == ReplicationSchemaConstants.AttemptTable.Name &&
                    t.TableDescription == ReplicationSchemaConstants.AttemptTable.Description &&
                    t.TableType == UserTableType.NoObject),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Tabla GNA_REP_ATTEMPT creada correctamente");
    }

    [Fact(DisplayName = "⏺ Campo RetryCount debe crearse en GNA_REP_QUEUE con tipo Numeric")]
    public async Task ExecuteAsync_ShouldCreateRetryCountFieldInQueue()
    {
        _output.WriteLine("🔄 INICIO: Validando campo RetryCount en GNA_REP_QUEUE");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.QueueTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.QueueTable.Fields.RetryCount &&
                    f.Description == "Reintentos realizados" &&
                    f.Type == UserFieldType.Numeric),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Campo RetryCount creado correctamente");
    }

    [Fact(DisplayName = "⏺ Debe ejecutar el número correcto de operaciones totales")]
    public async Task ExecuteAsync_ShouldCreateCorrectNumberOfTotalOperations()
    {
        _output.WriteLine("🔄 INICIO: Contando operaciones totales");

        // Arrange
        var ct = CancellationToken.None;
        var totalOperations = 0;

        _mockSchemaService
            .Setup(x => x.EnsureUserTableAsync(It.IsAny<UserTableDefinition>(), ct))
            .Callback(() =>
            {
                totalOperations++;
                _output.WriteLine("   📋 Operación #{0}: Crear tabla", totalOperations);
            })
            .Returns(Task.CompletedTask);

        _mockSchemaService
            .Setup(x => x.EnsureUserFieldAsync(It.IsAny<string>(), It.IsAny<UserFieldDefinition>(), ct))
            .Callback(() =>
            {
                totalOperations++;
                _output.WriteLine("   📊 Operación #{0}: Crear campo", totalOperations);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert - 2 tablas + 9 campos = 11 operaciones totales
        _output.WriteLine("📊 Total de operaciones ejecutadas: {0}", totalOperations);
        Assert.Equal(11, totalOperations);
        _output.WriteLine("✅ ÉXITO: Número de operaciones es correcto");
    }

    [Fact(DisplayName = "⏺ Debe manejar correctamente las excepciones del servicio")]
    public async Task ExecuteAsync_ShouldHandleServiceException()
    {
        _output.WriteLine("🔄 INICIO: Validando manejo de excepciones");

        // Arrange
        var ct = CancellationToken.None;
        _mockSchemaService
            .Setup(x => x.EnsureUserTableAsync(It.IsAny<UserTableDefinition>(), ct))
            .ThrowsAsync(new InvalidOperationException("Schema creation failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(ct));
        _output.WriteLine("✅ ÉXITO: Excepción capturada correctamente");
    }

    [Fact(DisplayName = "⏺ Debe manejar OperationCanceledException")]
    public async Task ExecuteAsync_ShouldHandleOperationCanceledException()
    {
        _output.WriteLine("🔄 INICIO: Validando manejo de OperationCanceledException");

        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ct = cts.Token;

        _mockSchemaService
            .Setup(x => x.EnsureUserTableAsync(It.IsAny<UserTableDefinition>(), ct))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _useCase.ExecuteAsync(ct));
        _output.WriteLine("✅ ÉXITO: OperationCanceledException capturada correctamente");
    }
}