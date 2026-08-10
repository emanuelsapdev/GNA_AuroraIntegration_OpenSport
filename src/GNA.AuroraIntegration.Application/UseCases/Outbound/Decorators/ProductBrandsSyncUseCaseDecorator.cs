using GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound.Decorators
{
    public sealed class ProductBrandsSyncUseCaseDecorator : IProductBrandsSyncUseCase
    {
        private readonly IProductBrandsSyncUseCase _inner;
        private readonly ILogger<ProductBrandsSyncUseCaseDecorator> _logger;
        public ProductBrandsSyncUseCaseDecorator(
            IProductBrandsSyncUseCase inner,
            ILogger<ProductBrandsSyncUseCaseDecorator> logger)
        {
            _inner = inner;
            _logger = logger;
        }
        public async Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Iniciando pipeline de sincronización de marcas de productos.");
            (int processed, int successful, int failed) result = await _inner.ExecuteAsync(ct);
            _logger.LogInformation(
                "Pipeline de sincronización finalizado. Procesados: {Processed}, exitosos: {Successful}, fallidos: {Failed}.",
                result.processed,
                result.successful,
                result.failed);
            return result;
        }

    }
}
