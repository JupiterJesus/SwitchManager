using System;
using System.Runtime.Serialization;

namespace SwitchManager.nx.system
{
    [Serializable]
    public class BadNcaException : Exception
    {
        public string NcaFile { get; set; }

        public BadNcaException()
        {
        }

        public BadNcaException(string message) : base(message)
        {
        }

        public BadNcaException(string ncaFile, string message) : base(message)
        {
            this.NcaFile = ncaFile;
        }

        public BadNcaException(string ncaFile, string message, Exception innerException) : base(message, innerException)
        {
            this.NcaFile = ncaFile;
        }

        public BadNcaException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BadNcaException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}