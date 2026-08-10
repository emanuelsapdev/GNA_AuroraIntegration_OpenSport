using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Application.UseCases.Outbound.Interfaces
{
    public interface IProductBrandsSyncUseCase
    {
        Task<(int processed, int successful, int failed)> ExecuteAsync(CancellationToken ct = default);
    }
}
