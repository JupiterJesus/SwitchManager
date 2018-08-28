using System;
using System.Runtime.Serialization;

namespace SwitchManager.nx.cdn
{
    [Serializable]
    internal class CnmtMissingException : Exception
    {
        public CnmtMissingException()
        {
        }

        public CnmtMissingException(string message) : base(message)
        {
        }

        public CnmtMissingException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CnmtMissingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}