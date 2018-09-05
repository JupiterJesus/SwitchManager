using System;
using System.Runtime.Serialization;

namespace SwitchManager.nx.system
{
    [Serializable]
    internal class InvalidNspException : Exception
    {
        public InvalidNspException()
        {
        }

        public InvalidNspException(string message) : base(message)
        {
        }

        public InvalidNspException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidNspException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}