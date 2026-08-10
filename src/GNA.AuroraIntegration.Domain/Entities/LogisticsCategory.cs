using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Domain.Entities
{
    public sealed class LogisticsCategory
    {
        public required string Name { get; init; }
        public required string Code { get; init; }
        public string Description { get; init; }
    }
}
