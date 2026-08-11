using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace GNA.AuroraIntegration.Infrastructure.ServiceLayer.DTOs
{
    /// <summary>Envoltorio genérico de colección OData de Service Layer.</summary>
    public sealed class ServiceLayerCollectionDto<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }
}
