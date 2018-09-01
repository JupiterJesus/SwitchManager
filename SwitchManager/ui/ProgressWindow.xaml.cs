using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SwitchManager.nx.library;
using System.Globalization;
using SwitchManager.util;
using System.Threading.Tasks;
using SwitchManager.io;
using log4net;

namespace SwitchManager.ui
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ProgressWindow));

        private SwitchLibrary library;
        private Dictionary<ProgressJob, JobTracker> jobs = new Dictionary<ProgressJob, JobTracker>();

        public ProgressWindow(SwitchLibrary library)
        {
            InitializeComponent();
            this.library = library;
            
            ProgressJob.DownloadStarted += JobStarted;
            //ProgressJob.DownloadProgress += JobProgress;
            ProgressJob.DownloadFinished += JobFinished;
            
        }

        #region Download Progress
        
        private void JobStarted(ProgressJob job)
        {
            JobTracker tracker = new JobTracker { Job = job };
            jobs.Add(job, tracker);

            Dispatcher?.InvokeOrExecute(delegate 
            {
                // New progress bar
                ProgressBar bar = new ProgressBar
                {
                    Minimum=0, Maximum=job.ExpectedSize,
                    Height=25, Name = $"ProgressBar_{jobs.Count - 1}",
                };
                    
                // Bind the Progress value to the Value property
                bar.SetBinding(ProgressBar.ValueProperty, 
                    new Binding("ProgressCompleted")
                    {
                        Source = job,
                        Mode = BindingMode.OneWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    });
                bar.MouseDoubleClick += (s, a) => job.Cancel();

                TextBlock t = new TextBlock
                {
                    Name = $"ProgressLabel_{jobs.Count - 1}",
                };
                t.SetBinding(TextBlock.TextProperty,
                    new Binding("ProgressSinceLastUpdate")
                    {
                        Source = job,
                        Mode = BindingMode.OneWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                        Converter = new DownloadProgressTextConverter(job)
                    });

                tracker.Container = new StackPanel ();
                tracker.Container.Children.Add(bar);
                tracker.Container.Children.Add(t);
                tracker.Container.UpdateLayout();

                DownloadsPanel.Children.Insert(0, tracker.Container);
                DownloadsPanel.ScrollOwner?.ScrollToTop();
                DownloadsPanel.UpdateLayout();
            });

            if (job is FileWriteJob dj)
            {
                string kind = dj is DownloadJob ? "download" : "file write";
                if (job.ProgressCompleted == 0)
                    logger.Info($"Starting {kind} of size {Miscellaneous.ToFileSize(job.ExpectedSize)}, File: '{dj.FileName}'.");
                else
                    logger.Info($"Resuming {kind} at {Miscellaneous.ToFileSize(job.ProgressCompleted)}/{Miscellaneous.ToFileSize(job.ExpectedSize)}, File: '{dj.FileName}'.");
            }
            else
                logger.Info($"Starting job '{job.JobName}'");
        }

        private void JobFinished(ProgressJob job)
        {
            JobTracker j = jobs[job];

            //downloads.Remove(download.FileName);

            Dispatcher?.InvokeOrExecute(async delegate
            {
                while (j.Container == null)
                    await Task.Delay(100);
                DownloadsPanel.Children.Remove(j.Container);
                DownloadsPanel.Children.Insert(DownloadsPanel.Children.Count, j.Container);
            });

            if (job is DownloadJob dl)
                logger.Info($"Finished downloading file '{dl.FileName}'.");
            else
                logger.Info($"Finished task '{job.JobName}'.");
        }

        private void JobProgress(ProgressJob download, int progressSinceLast)
        {
            //Download dl = downloads[download.FileName];
        }

        #endregion

        #region JobTracker (helper for tracking jobs)

        private struct JobTracker
        {
            public ProgressJob Job { get; set; }
            public Panel Container { get; set; }
        }

        #endregion

        private void Button_Clear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var d in jobs.Values)
            {
                if (d.Job.IsComplete)
                {
                    jobs.Remove(d.Job);
                    Dispatcher?.InvokeOrExecute(async delegate
                    {
                        while (d.Container == null)
                            await Task.Delay(100);
                        DownloadsPanel.Children.Remove(d.Container);
                    });
                }
            }
        }
    }

    public class DownloadProgressTextConverter : IValueConverter
    {
        private DateTime completed;

        ProgressJob job = null;
        public DownloadProgressTextConverter(ProgressJob job)
        {
            this.job = job;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long progressSinceLast = (long)value; 

            if (job.IsComplete)
            {
                if (this.completed == default(DateTime)) // bugfix, it cant be null it is a value type apparently, so always would say min-value 1/1/0001 if you check against null
                    this.completed = DateTime.Now;

                if (job is FileWriteJob dj)
                    return $"{dj.JobName}\n{dj.FileName}\nCompleted on {this.completed.ToLongDateString()}";
                else
                    return $"Task \"{job.JobName}\" finished! Completed on {this.completed.ToLongDateString()}";
            }
            else if (job.ProgressCompleted > 0)
            {
                if (job is FileWriteJob dj)
                {
                    double speed = 0;// job.ProgressSpeed;

                    string ts = speed == 0 ? "Unknown Date" : DateTime.Now.AddSeconds(job.ProgressRemaining / speed).ToLongDateString();

                    return $"{dj.JobName}\n{dj.FileName}\n{Miscellaneous.ToFileSize(job.ProgressCompleted)} / {Miscellaneous.ToFileSize(job.ExpectedSize)}  -  {Miscellaneous.ToFileSize(speed)} / sec (avg) - Complete on {ts}\nDouble-click progress bar to cancel";
                }
                else
                {
                    double percent = job.PercentCompleted;
                    return $"Task \"{job.JobName}\" is {percent:P1} complete.";
                }
            }
            else
            {
                if (job is FileWriteJob dj)
                    return $"{dj.JobName}\n{dj.FileName}\n{Miscellaneous.ToFileSize(job.ProgressCompleted)} / {Miscellaneous.ToFileSize(job.ExpectedSize)}\nDouble-click progress bar to cancel";
                else
                   return $"Task \"{job.JobName}\" is starting.";
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
