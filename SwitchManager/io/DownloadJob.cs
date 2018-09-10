using SwitchManager.nx.system;
using System;
using System.ComponentModel;
using System.IO;

namespace SwitchManager.io
{
    public class DownloadJob : FileWriteJob
    {
        public Stream SourceStream { get; set; }

        #region Constructors

        public DownloadJob(Stream webStream, FileStream fileStream, string jobName, long expectedSize, long startingSize = 0) : base(fileStream, jobName, expectedSize, startingSize)
        {
            this.SourceStream = webStream;
        }


        #endregion
    }
}