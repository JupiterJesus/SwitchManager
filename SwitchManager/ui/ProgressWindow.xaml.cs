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

namespace SwitchManager.ui
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
            dl.Task = download;
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
                        Name = $"ProgressLabel_{downloads.Count - 1}",
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
                    DownloadsPanel.Children.Insert(1, dl.Container);
                    DownloadsPanel.UpdateLayout();
                });

            if (download.Progress == 0)
                Console.WriteLine($"Starting download of size {Miscellaneous.ToFileSize(download.ExpectedSize)}, File: '{download.FileName}'.");
            else
                Console.WriteLine($"Resuming download at {Miscellaneous.ToFileSize(download.Progress)}/{Miscellaneous.ToFileSize(download.ExpectedSize)}, File: '{download.FileName}'.");
        }

        private void Downloader_DownloadFinished(DownloadTask download)
        {
            //Download dl = downloads[download.FileName];
            //downloads.Remove(download.FileName);

            //if (Application.Current != null)
            //    Application.Current.Dispatcher.Invoke((Action)delegate
            //    {
            //        //DownloadsPanel.Children.Remove(dl.Container);
           //     });

            //Console.WriteLine($"Finished download, File: '{download.FileName}'.");
        }

        private void Downloader_DownloadProgress(DownloadTask download, int progressSinceLast)
        {
            //Download dl = downloads[download.FileName];
        }

        #endregion
        private class Download
        {
            internal DownloadTask Task { get; set; }
            internal Panel Container { get; set; }
        }

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var d in downloads)
            {
                if (d.Value.Task.IsComplete)
                {
                    downloads.Remove(d.Key);
                    if (Application.Current != null && d.Value.Container != null)
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                             DownloadsPanel.Children.Remove(d.Value.Container);
                        });
                }
            }
        }
    }

    public class DownloadProgressTextConverter : IValueConverter
    {
        private readonly long expected;
        private readonly string filename;
        private Stopwatch clock = new Stopwatch();
        private DateTime completed;

        public DownloadProgressTextConverter(long expected, string filename)
        {
            this.expected = expected;
            this.filename = filename;
            this.clock.Restart();
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long progress = (long)value; 

            if (progress == expected)
            {
                if (this.completed == default(DateTime)) // bugfix, it cant be null it is a value type apparently, so always would say min-value 1/1/0001 if you check against null
                    this.completed = DateTime.Now;

                return $"{filename}\nCompleted on {this.completed.ToLongDateString()}";
            }
            else if (progress > 0)
            {
                double speed = ((double)progress / clock.Elapsed.TotalSeconds);
                long left = expected - progress;
                double remainingSeconds = left / speed;

                DateTime time = DateTime.Now.AddSeconds(remainingSeconds);
                return $"{filename}\n{Miscellaneous.ToFileSize(progress)} / {Miscellaneous.ToFileSize(expected)}  -  {Miscellaneous.ToFileSize(speed)} / sec (avg) - Complete on {time.ToLongTimeString()}\nDouble-click progress bar to cancel";    
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
