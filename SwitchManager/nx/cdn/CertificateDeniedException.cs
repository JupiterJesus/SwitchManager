using System;
using System.Runtime.Serialization;

namespace SwitchManager.nx.cdn
{
    [Serializable]
    internal class CertificateDeniedException : Exception
    {
        public CertificateDeniedException()
        {
        }

        public CertificateDeniedException(string message) : base(message)
        {
        }

        public CertificateDeniedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CertificateDeniedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}