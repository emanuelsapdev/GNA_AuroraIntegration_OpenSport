using GNA.AuroraIntegration.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Domain.Exceptions
{
    public sealed class ReplicationControlStoreException : AuroraIntegrationException
    {
        private readonly ReplicableEntityType entityType;
        private readonly string entityKey;
        public ReplicationControlStoreException(ReplicableEntityType entityType, string entityKey, string message) : base(message)
        {
            this.entityType = entityType;
            this.entityKey = entityKey;
        }
        public ReplicationControlStoreException(ReplicableEntityType entityType, string entityKey, string message, Exception inner) : base(message, inner)
        {
            this.entityType = entityType;
            this.entityKey = entityKey;
        }
    }
}
