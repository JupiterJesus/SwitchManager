using SwitchManager.nx.system;
using System.ComponentModel;
using System.IO;

namespace SwitchManager.nx.cdn
{
    public class DownloadTask : INotifyPropertyChanged
    {
        public Stream WebStream { get; set; }
        public FileStream FileStream { get; set; }
        public long ExpectedSize { get; set; }
        public long BytesDownloaded { get; private set; }
        public long BytesLeft { get { return ExpectedSize - BytesDownloaded; } }
        public string FileName {  get { return FileStream?.Name ?? null; } }
        public bool IsCanceled { get; private set; } = false;
        public bool IsComplete { get { return BytesDownloaded == ExpectedSize; } }
        public SwitchTitle Title { get; set; }

        public DownloadTask(Stream webStream, FileStream fileStream, long expectedSize, long startingSize = 0)
        {
            this.WebStream = webStream;
            this.FileStream = fileStream;
            this.ExpectedSize = expectedSize;
            this.BytesDownloaded = startingSize;
        }

        public void UpdateProgress(int progress)
        {
            this.BytesDownloaded += progress;
            NotifyPropertyChanged("BytesDownloaded");
        }

        public void Cancel()
        {
            this.IsCanceled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }
}