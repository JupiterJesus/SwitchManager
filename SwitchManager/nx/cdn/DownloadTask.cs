using System.ComponentModel;
using System.IO;

namespace SwitchManager.nx.cdn
{
    public class DownloadTask : INotifyPropertyChanged
    {
        public Stream WebStream { get; set; }
        public FileStream FileStream { get; set; }
        public long ExpectedSize { get; set; }
        public long Progress { get; private set; }
        public string FileName {  get { return FileStream?.Name ?? null; } }
        public bool IsCanceled { get; private set; } = false;

        public DownloadTask(Stream webStream, FileStream fileStream, long expectedSize, long startingSize = 0)
        {
            this.WebStream = webStream;
            this.FileStream = fileStream;
            this.ExpectedSize = expectedSize;
            this.Progress = startingSize;
        }

        public void UpdateProgress(int progress)
        {
            this.Progress += progress;
            NotifyPropertyChanged("Progress");
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