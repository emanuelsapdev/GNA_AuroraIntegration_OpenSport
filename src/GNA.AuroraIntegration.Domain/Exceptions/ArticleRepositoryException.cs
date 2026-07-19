using System;
using System.Collections.Generic;
using System.Text;

namespace GNA.AuroraIntegration.Domain.Exceptions
{
    public sealed class ArticleRepositoryException : AuroraIntegrationException
    {
        public ArticleRepositoryException(string message)
        : base(message) { }

        public ArticleRepositoryException(string message, Exception inner)
            : base(message, inner) { }

    }
}
