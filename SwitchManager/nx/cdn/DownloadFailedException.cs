using System;
using System.Runtime.Serialization;

namespace SwitchManager.nx.cdn
{
    [Serializable]
    internal class DownloadFailedException : Exception
    {
        public DownloadFailedException()
        {
        }

        public DownloadFailedException(string message) : base(message)
        {
        }

        public DownloadFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DownloadFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}