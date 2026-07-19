using GNA.AuroraIntegration.Application.DTOs.Aurora;
using GNA.AuroraIntegration.Application.Interfaces;
using GNA.AuroraIntegration.Application.UseCases.Outbound;
using GNA.AuroraIntegration.Domain.Entities;
using GNA.AuroraIntegration.Domain.Enums;
using GNA.AuroraIntegration.Domain.Exceptions;
using GNA.AuroraIntegration.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace GNA.AuroraIntegration.Tests
{
    /// <summary>
    /// Tests para validar la creación de las tablas y campos de replicación.
    /// </summary>
    public class ArticleSyncUseCaseTests
    {
        private readonly Mock<IArticleReplicationRepository> _repositoryMock;
        private readonly Mock<IAuroraArticleApiClient> _apiAuroraMock;
        private readonly Mock<ILogger<ArticleSyncUseCase>> _loggerMock;

        private readonly ArticleSyncUseCase _useCase;
        private readonly ITestOutputHelper _output;

        public ArticleSyncUseCaseTests(ITestOutputHelper output)
        {
            _repositoryMock = new Mock<IArticleReplicationRepository>();
            _apiAuroraMock = new Mock<IAuroraArticleApiClient>();
            _loggerMock = new Mock<ILogger<ArticleSyncUseCase>>();
            _useCase = new ArticleSyncUseCase(
                _repositoryMock.Object,
                _apiAuroraMock.Object,
                _loggerMock.Object
                );

            _output = output;
        }

        [Fact(DisplayName = "⏺ Test de sincronización de artículos")]
        public async Task ExecuteAsync_ShouldProcessPendingArticles()
        {
            _output.WriteLine("🔄 INICIO: Sincronizando artículos");
            // Arrange
            var pendingArticles = new List<Article>
            {
                new Article { Sku = "A001", Name = "Artículo 1", PrimaryEan = "1234567890123", WeightInGr = 100, HeightInCm = 10, WidthInCm = 5, LengthInCm = 20 },
                new Article { Sku = "A002", Name = "Artículo 2", PrimaryEan = "1234567890124", WeightInGr = 200, HeightInCm = 15, WidthInCm = 7, LengthInCm = 25 }
            };
            _repositoryMock.Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pendingArticles);
            _output.WriteLine($"📦 Artículos pendientes de sincronización: {pendingArticles.Count}");
            // Act
            var (processed, successful, failed) = await _useCase.ExecuteAsync();
            Assert.Equal(pendingArticles.Count, processed);
            Assert.Equal(pendingArticles.Count, successful);
            Assert.Equal(0, failed);

            // Assert
            _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(pendingArticles.Count));
            _output.WriteLine("💾 Artículos marcados como replicados");

            _output.WriteLine("✅ ÉXITO: Todos los artículos pendientes han sido procesados correctamente");
        }

        [Fact(DisplayName = "⏺ Test de manejo de excepción del cliente API")]
        public async Task ExecuteAsync_ShouldHandleApiClientException()
        {
            // Arrange
            var pendingArticles = new List<Article> // Simulamos un artículo pendiente 
            {
                new Article { Sku = "A001", Name = "Artículo 1", PrimaryEan = "1234567890123", WeightInGr = 100, HeightInCm = 10, WidthInCm = 5, LengthInCm = 20 }
            };

            _repositoryMock.Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())) // Simulamos que hay un artículo pendiente
                .ReturnsAsync(pendingArticles);
            _output.WriteLine($"📦 Artículos pendientes de sincronización: {pendingArticles.Count}");

            _apiAuroraMock.Setup(c => c.CreateArticleAsync(It.IsAny<CreateAuroraArticleDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArticleAuroraApiException(It.IsAny<string>(), "Simulated API failure")); // Simulamos una excepción al llamar al API
            _output.WriteLine("⚠️ Simulación de fallo en el cliente API al crear artículo");

            // Act
            var (processed, successful, failed) = await _useCase.ExecuteAsync(); // Ejecutamos el caso de uso

            // Assert
            Assert.Equal(1, processed); // Verificamos que se procesó un artículo
            Assert.Equal(0, successful); // Verificamos que no se procesó ningún artículo con éxito
            Assert.Equal(1, failed); // Verificamos que se marcó un artículo como fallido
            _output.WriteLine("❌ ERROR: No se procesaron artículos debido a la excepción del cliente API");

            _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // Verificamos que no se marcó ningún artículo como replicado
            _output.WriteLine("💾 Ningún artículo fue marcado como replicado debido a la excepción");

            _output.WriteLine("✅ ÉXITO: La excepción del cliente API fue manejada correctamente y no se procesaron artículos");
        }


        [Fact(DisplayName = "⏺ Test de manejo de excepción del repositorio")]
        public async Task ExecuteAsync_ShouldHandleRepositoryException()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated repository failure")); // Simulamos una excepción al llamar al repositorio
            _output.WriteLine("⚠️ Simulación de fallo en el repositorio al obtener artículos pendientes");
            // Act
            var (processed, successful, failed) = await _useCase.ExecuteAsync(); // Ejecutamos el caso de uso
            // Assert
            Assert.Equal(0, processed); // Verificamos que no se procesó ningún artículo debido a la excepción
            Assert.Equal(0, successful); // Verificamos que no se procesó ningún artículo con éxito
            Assert.Equal(0, failed); // Verificamos que no se marcó ningún artículo como fallido
            _output.WriteLine("❌ ERROR: No se procesaron artículos debido a la excepción del repositorio");
            _repositoryMock.Verify(r => r.MarkArticleAsReplicatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            // Verificamos que no se marcó ningún artículo como replicado
            _output.WriteLine("💾 Ningún artículo fue marcado como replicado debido a la excepción");
            _output.WriteLine("✅ ÉXITO: La excepción del repositorio fue manejada correctamente y no se procesaron artículos");
        }

        [Fact(DisplayName = "⏺ Verificar que ante fallo de Aurora se llama a MarkArticleAsFailedAsync")]
        public async Task ExecuteAsync_ShouldCallMarkArticleAsFailedAsyncOnAuroraFailure()
        {
            // Arrange
            var pendingArticles = new List<Article>
            {
                new Article { Sku = "A001", Name = "Artículo 1", PrimaryEan = "1234567890123", WeightInGr = 100, HeightInCm = 10, WidthInCm = 5, LengthInCm = 20 }
            };
            _repositoryMock.Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pendingArticles);
            _output.WriteLine($"📦 Artículos pendientes de sincronización: {pendingArticles.Count}");

            _apiAuroraMock.Setup(c => c.CreateArticleAsync(It.IsAny<CreateAuroraArticleDto>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArticleAuroraApiException("A001", "Simulated API failure"));
            _output.WriteLine("⚠️ Simulación de fallo en Aurora al crear artículo, se espera que se llame a MarkArticleAsFailedAsync");

            // Act
            var (processed, successful, failed) = await _useCase.ExecuteAsync();

            // Assert
            _repositoryMock.Verify(r => r.MarkArticleAsFailedAsync("A001", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _output.WriteLine("💾 Se llamó a MarkArticleAsFailedAsync para el artículo con fallo en Aurora");

            _output.WriteLine("✅ ÉXITO: La llamada a MarkArticleAsFailedAsync se realizó correctamente ante el fallo de Aurora");
        }

        [Fact(DisplayName = "⏺ Lote mixto: algunos artículos exitosos y otros fallidos en la misma ejecución")]
        public async Task ExecuteAsync_ShouldProcessMixedBatch()
        {
            // Arrange
            var pendingArticles = new List<Article>
            {
                new Article { Sku = "A001", Name = "Artículo 1", PrimaryEan = "1234567890123", WeightInGr = 100, HeightInCm = 10, WidthInCm = 5, LengthInCm = 20 },
                new Article { Sku = "A002", Name = "Artículo 2", PrimaryEan = "1234567890124", WeightInGr = 200, HeightInCm = 15, WidthInCm = 7, LengthInCm = 25 }
            };
            _repositoryMock.Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(pendingArticles);
            _output.WriteLine($"📦 Artículos pendientes de sincronización: {pendingArticles.Count}");
            _apiAuroraMock.Setup(c => c.CreateArticleAsync(It.Is<CreateAuroraArticleDto>(dto => dto.Sku == "A001"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask); // A001 se procesa correctamente
            _apiAuroraMock.Setup(c => c.CreateArticleAsync(It.Is<CreateAuroraArticleDto>(dto => dto.Sku == "A002"), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArticleAuroraApiException("A002", "Simulated API failure")); // A002 falla
            // Act
            var (processed, successful, failed) = await _useCase.ExecuteAsync();
            // Assert
            Assert.Equal(2, processed);
            Assert.Equal(1, successful);
            Assert.Equal(1, failed);
            _output.WriteLine("✅ ÉXITO: Lote mixto procesado correctamente con resultados esperados");
        }

        [Fact(DisplayName = "⏺ Lista de pendientes vacía")]
        public async Task ExecuteAsync_ShouldHandleEmptyPendingList()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetPendingArticlesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Article>()); // Lista vacía
            _output.WriteLine("📦 No hay artículos pendientes de sincronización");
            // Act
            var (processed, successful, failed) = await _useCase.ExecuteAsync();
            // Assert
            Assert.Equal(0, processed);
            Assert.Equal(0, successful);
            Assert.Equal(0, failed);
            _output.WriteLine("✅ ÉXITO: Lista de pendientes vacía manejada correctamente sin errores");
        }
    }
}
