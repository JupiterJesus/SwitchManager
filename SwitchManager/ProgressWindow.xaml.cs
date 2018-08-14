using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SwitchManager.nx;
using SwitchManager.nx.library;
using SwitchManager.nx.cdn;
using SwitchManager.Properties;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using SwitchManager.util;
using System.Diagnostics;

namespace SwitchManager
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private SwitchLibrary library;
        private Dictionary<string, Download> downloads = new Dictionary<string, Download>();

        public ProgressWindow(SwitchLibrary library)
        {
            InitializeComponent();
            this.library = library;
            
            library.Loader.DownloadStarted += Downloader_DownloadStarted;
            library.Loader.DownloadProgress += Downloader_DownloadProgress;
            library.Loader.DownloadFinished += Downloader_DownloadFinished;
            
        }

        #region Download Progress
        
        private void Downloader_DownloadStarted(DownloadTask download)
        {
            Download dl = new Download();
            downloads.Add(download.FileName, dl);
            if (Application.Current != null)
                Application.Current.Dispatcher.Invoke((Action)delegate 
                {
                    // New progress bar
                    ProgressBar bar = new ProgressBar
                    {
                        Minimum=0, Maximum=download.ExpectedSize,
                        Height=25, Name = $"ProgressBar_{downloads.Count - 1}",
                    };

                    // Bind the Progress value to the Value property
                    bar.SetBinding(ProgressBar.ValueProperty, 
                        new Binding("Progress")
                        {
                            Source = download,
                            Mode = BindingMode.OneWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        });
                    bar.MouseDoubleClick += (s, a) => download.Cancel();

                    TextBlock t = new TextBlock
                    {
                        Name = $"ProgressLable_{downloads.Count - 1}",
                    };
                    t.SetBinding(TextBlock.TextProperty,
                        new Binding("Progress")
                        {
                            Source = download,
                            Mode = BindingMode.OneWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                            Converter = new DownloadProgressTextConverter(download.ExpectedSize, download.FileName)
                        });

                    dl.Container = new StackPanel ();
                    dl.Container.Children.Add(bar);
                    dl.Container.Children.Add(t);
                    dl.Container.UpdateLayout();
                    DownloadsPanel.Children.Add(dl.Container);
                    DownloadsPanel.UpdateLayout();
                });

            if (download.Progress == 0)
                Console.WriteLine($"Starting download of size {Miscellaneous.ToFileSize(download.ExpectedSize)}, File: '{download.FileName}'.");
            else
                Console.WriteLine($"Resuming download at {Miscellaneous.ToFileSize(download.Progress)}/{Miscellaneous.ToFileSize(download.ExpectedSize)}, File: '{download.FileName}'.");
        }

        private void Downloader_DownloadFinished(DownloadTask download)
        {
            Download dl = downloads[download.FileName];
            downloads.Remove(download.FileName);

            if (Application.Current != null)
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    DownloadsPanel.Children.Remove(dl.Container);
                });

            Console.WriteLine($"Finished download, File: '{download.FileName}'.");
        }

        private void Downloader_DownloadProgress(DownloadTask download, int progressSinceLast)
        {
            Download dl = downloads[download.FileName];
        }

        #endregion
        private class Download
        {
            internal DownloadTask Task { get; set; }
            internal Panel Container { get; set; }
        }

    }
    public class DownloadProgressTextConverter : IValueConverter
    {
        private long expected;
        private string filename;
        private Stopwatch Clock { get; set; } = new Stopwatch();
        public DownloadProgressTextConverter(long expected, string filename)
        {
            this.expected = expected;
            this.filename = filename;
            this.Clock.Restart();
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long progress = (long)value; 
            if (progress > 0)
            {
                double speed = ((double)progress / Clock.Elapsed.TotalSeconds);
                long left = expected - progress;
                double remainingSeconds = left / speed;

                DateTime time = DateTime.Now.AddSeconds(remainingSeconds);
                return $"{filename}\n{Miscellaneous.ToFileSize(progress)} / {Miscellaneous.ToFileSize(expected)}  -  {Miscellaneous.ToFileSize(speed)} / sec - Complete on {time.ToShortTimeString()}\nDouble-click progress bar to cancel";    
            }
            else
            {
                return $"{filename}\n{Miscellaneous.ToFileSize(progress)} / {Miscellaneous.ToFileSize(expected)}\nDouble-click progress bar to cancel";
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
