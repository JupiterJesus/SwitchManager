using SwitchManager.nx.system;
using System;
using System.ComponentModel;
using System.IO;

namespace SwitchManager.io
{
    public class FileWriteJob : ProgressJob
    {
        public FileStream DestinationStream { get; set; }
        public string FileName { get { return DestinationStream?.Name ?? null; } }

        #region Constructors

        public FileWriteJob(FileStream destinationStream, string jobName, long expectedSize, long startingSize = 0) : base(jobName, expectedSize, startingSize)
        {
            this.DestinationStream = destinationStream;
        }

        #endregion
    }
}