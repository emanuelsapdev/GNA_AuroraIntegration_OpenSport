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
/// Tests para validar la creación de las tablas, campos y objetos de usuario del esquema de replicación.
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

    [Fact(DisplayName = "✓ Debe crear la tabla GNA_AUR_REP_QUEUE con los parametros esperados.")]
    public async Task ExecuteAsync_ShouldCreateQueueTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GNA_AUR_REP_QUEUE");

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

        _output.WriteLine("✅ ÉXITO: Tabla GNA_AUR_REP_QUEUE creada correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear los campos en la tabla GNA_AUR_REP_QUEUE.")]
    public async Task ExecuteAsync_ShouldCreateQueueTableFields()
    {
        _output.WriteLine("🔄 INICIO: Validando campos en tabla GNA_AUR_REP_QUEUE");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert - EntityType
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.QueueTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.QueueTable.Fields.EntityType &&
                    f.Type == UserFieldType.Alpha),
                ct),
            Times.Once);

        // Assert - EntityKey
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.QueueTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.QueueTable.Fields.EntityKey &&
                    f.Type == UserFieldType.Alpha),
                ct),
            Times.Once);

        // Assert - Operation
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.QueueTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.QueueTable.Fields.Operation &&
                    f.Type == UserFieldType.Alpha),
                ct),
            Times.Once);

        // Assert - Status
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.QueueTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.QueueTable.Fields.Status &&
                    f.Type == UserFieldType.Alpha),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Todos los campos de GNA_AUR_REP_QUEUE creados correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear la tabla GNA_AUR_REP_ATTEMPT con los parámetros esperados.")]
    public async Task ExecuteAsync_ShouldCreateAttemptTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GNA_AUR_REP_ATTEMPT");

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

    [Fact(DisplayName = "✓ Campo RetryCount debe crearse en GNA_AUR_REP_QUEUE con tipo Numeric")]
    public async Task ExecuteAsync_ShouldCreateRetryCountFieldInQueue()
    {
        _output.WriteLine("🔄 INICIO: Validando campo RetryCount en GNA_AUR_REP_QUEUE");

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

    [Fact(DisplayName = "✓ Debe crear la tabla GN_CATLOG (Categorías Logísticas)")]
    public async Task ExecuteAsync_ShouldCreateLogisticsCategoryTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GN_CATLOG");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserTableAsync(
                It.Is<UserTableDefinition>(t =>
                    t.TableName == ReplicationSchemaConstants.LogisticsCategoryTable.Name &&
                    t.TableDescription == ReplicationSchemaConstants.LogisticsCategoryTable.Description &&
                    t.TableType == UserTableType.MasterData),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Tabla GN_CATLOG creada correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear el objeto de usuario para Categorías Logísticas")]
    public async Task ExecuteAsync_ShouldCreateLogisticsCategoryUserObject()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de objeto usuario para Categorías");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserObjectAsync(
                It.Is<UserObjectDefinition>(o =>
                    o.Code == ReplicationSchemaConstants.LogisticsCategoryUserObject.Code &&
                    o.TableName == ReplicationSchemaConstants.LogisticsCategoryUserObject.TableName),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Objeto usuario para Categorías creado correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear la tabla GN_MARCAS (Marcas de Productos)")]
    public async Task ExecuteAsync_ShouldCreateProductBrandsTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GN_MARCAS");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserTableAsync(
                It.Is<UserTableDefinition>(t =>
                    t.TableName == ReplicationSchemaConstants.ProductBrandsTable.Name &&
                    t.TableDescription == ReplicationSchemaConstants.ProductBrandsTable.Description &&
                    t.TableType == UserTableType.MasterData),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Tabla GN_MARCAS creada correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear el objeto de usuario para Marcas de Productos")]
    public async Task ExecuteAsync_ShouldCreateProductBrandsUserObject()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de objeto usuario para Marcas");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserObjectAsync(
                It.Is<UserObjectDefinition>(o =>
                    o.Code == ReplicationSchemaConstants.ProductBrandsUserObject.Code &&
                    o.TableName == ReplicationSchemaConstants.ProductBrandsUserObject.TableName),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Objeto usuario para Marcas creado correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear la tabla GNA_AUR_BANNERS (Banners)")]
    public async Task ExecuteAsync_ShouldCreateBannersTable()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de tabla GNA_AUR_BANNERS");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserTableAsync(
                It.Is<UserTableDefinition>(t =>
                    t.TableName == ReplicationSchemaConstants.BannersTable.Name &&
                    t.TableDescription == ReplicationSchemaConstants.BannersTable.Description &&
                    t.TableType == UserTableType.MasterData),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Tabla GNA_AUR_BANNERS creada correctamente");
    }

    [Fact(DisplayName = "✓ Debe crear el objeto de usuario para Banners")]
    public async Task ExecuteAsync_ShouldCreateBannersUserObject()
    {
        _output.WriteLine("🔄 INICIO: Validando creación de objeto usuario para Banners");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        _mockSchemaService.Verify(
            x => x.EnsureUserObjectAsync(
                It.Is<UserObjectDefinition>(o =>
                    o.Code == ReplicationSchemaConstants.BannersUserObject.Code &&
                    o.TableName == ReplicationSchemaConstants.BannersUserObject.TableName),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Objeto usuario para Banners creado correctamente");
    }

    [Fact(DisplayName = "✓ No debe crear campos (UDF) adicionales para GNA_AUR_BANNERS")]
    public async Task ExecuteAsync_ShouldNotCreateExtraFieldsForBannersTable()
    {
        _output.WriteLine("🔄 INICIO: Validando que GNA_AUR_BANNERS no reciba UDFs adicionales");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert - la tabla de Banners solo usa Code/Name por defecto, nunca se le agregan UDFs.
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.BannersTable.DbName,
                It.IsAny<UserFieldDefinition>(),
                ct),
            Times.Never);

        _output.WriteLine("✅ ÉXITO: GNA_AUR_BANNERS no recibió UDFs adicionales");
    }

    [Fact(DisplayName = "✓ Debe crear campos de Categoría Logística y Marca en OITM")]
    public async Task ExecuteAsync_ShouldCreateArticlesFields()
    {
        _output.WriteLine("🔄 INICIO: Validando campos en tabla OITM");

        // Arrange
        var ct = CancellationToken.None;

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert - LogisticsCategory field
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.ItemsTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.ItemsTable.Fields.LogisticsCategory &&
                    f.Type == UserFieldType.Alpha),
                ct),
            Times.Once);

        // Assert - ProductBrand field
        _mockSchemaService.Verify(
            x => x.EnsureUserFieldAsync(
                ReplicationSchemaConstants.ItemsTable.DbName,
                It.Is<UserFieldDefinition>(f =>
                    f.Name == ReplicationSchemaConstants.ItemsTable.Fields.ProductBrand &&
                    f.Type == UserFieldType.Alpha),
                ct),
            Times.Once);

        _output.WriteLine("✅ ÉXITO: Campos en OITM creados correctamente");
    }

    [Fact(DisplayName = "✓ Debe ejecutar el número correcto de operaciones totales")]
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

        _mockSchemaService
            .Setup(x => x.EnsureUserObjectAsync(It.IsAny<UserObjectDefinition>(), ct))
            .Callback(() =>
            {
                totalOperations++;
                _output.WriteLine("   📋 Operación #{0}: Crear objeto usuario", totalOperations);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _useCase.ExecuteAsync(ct);

        // Assert
        // 5 tablas + 14 campos + 3 objetos usuario = 22 operaciones totales
        // - QueueTable: 1 tabla + 5 campos
        // - AttemptTable: 1 tabla + 4 campos
        // - LogisticsCategoryTable: 1 tabla + 1 user object
        // - ProductBrandsTable: 1 tabla + 1 user object
        // - BannersTable: 1 tabla + 1 user object (sin campos adicionales, solo Code/Name por defecto)
        // - ItemsTable: 5 campos
        _output.WriteLine("📊 Total de operaciones ejecutadas: {0}", totalOperations);
        Assert.Equal(22, totalOperations);
        _output.WriteLine("✅ ÉXITO: Número de operaciones es correcto");
    }

    [Fact(DisplayName = "✓ Debe manejar correctamente las excepciones del servicio")]
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

    [Fact(DisplayName = "✓ Debe manejar OperationCanceledException")]
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
