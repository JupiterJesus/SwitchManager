using System;
using System.Runtime.Serialization;

namespace SwitchManager.util
{
    [Serializable]
    internal class HactoolFailedException : Exception
    {
        public HactoolFailedException()
        {
        }

        public HactoolFailedException(string message) : base(message)
        {
        }

        public HactoolFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected HactoolFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}