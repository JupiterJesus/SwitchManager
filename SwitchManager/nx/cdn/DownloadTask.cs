using System.IO;

namespace SwitchManager.nx.cdn
{
    public class DownloadTask
    {
        public Stream WebStream { get; set; }
        public FileStream FileStream { get; set; }
        public long ExpectedSize { get; set; }
        public long Progress { get; set; }
        public string FileName {  get { return FileStream?.Name ?? null; } }
        public DownloadTask(Stream webStream, FileStream fileStream, long expectedSize)
        {
            this.WebStream = webStream;
            this.FileStream = fileStream;
            this.ExpectedSize = expectedSize;
            this.Progress = 0;
        }

        public void UpdateProgress(int progress)
        {
            this.Progress += progress;
        }
    }
}