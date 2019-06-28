using SwitchManager.nx.system;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace SwitchManager.io
{
    public enum ProgressJobStatus
    {
        NOT_STARTED,
        RUNNING,
        PAUSED,
        STOPPED,
        FAILED,
        COMPLETE,
    }

    public class ProgressJob : INotifyPropertyChanged
    {
        #region Properties

        public long ExpectedSize { get; set; }
        public long ProgressCompleted { get; private set; }
        public long ProgressSinceLastUpdate { get; set; }
        public string JobName { get; set; }
        public ProgressJobStatus Status { get; set; } = ProgressJobStatus.NOT_STARTED;

        #endregion

        #region Read-only properties (informational only)

        public long ProgressRemaining { get { return ExpectedSize - ProgressCompleted; } }
        public double PercentCompleted { get { return ((double)ProgressCompleted) / ExpectedSize; } }
        public bool IsComplete { get { return ProgressCompleted == ExpectedSize; } }

        #endregion

        #region ProgressSpeed

        private DateTime speed_lastUpdateCalculated;
        private long speed_progress;
        private double cachedSpeed;
        private Queue<Tuple<DateTime, long>> changes = new Queue<Tuple<DateTime, long>>();

        /// <summary>
        /// Gets the speed of this task's progress. It only updates speed every half second at most, and
        /// produces a running average of the last 50 updates.
        /// </summary>
        public double ProgressSpeed
        {
            get
            {
                if (DateTime.Now >= speed_lastUpdateCalculated + TimeSpan.FromMilliseconds(500))
                {
                    cachedSpeed = GetRate();
                    speed_lastUpdateCalculated = DateTime.Now;
                    speed_progress = 0;
                }

                return cachedSpeed;
            }
        }

        private double GetRate()
        {
            if (changes.Count == 0)
                return 0;

            // time between most recent update and oldest tracked update
            TimeSpan timespan = changes.Last().Item1 - changes.First().Item1;

            // total progress over that time span
            long p = changes.Sum(t => t.Item2);
            /*
            long p = 0;
            var node = changes.First;
            while (node != null)
            {
                p += node.Value.Item2;
                node = node.Next;
            }
            */

            // average progress per second over that time span
            double rate = p / timespan.TotalSeconds;

            if (double.IsInfinity(rate) || double.IsNaN(rate))
                return 0;

            return rate;
        }

        #endregion

        public bool IsCancelled { get; private set; } = false;

        #region Constructors

        public ProgressJob(string jobName, long expectedSize, long startingSize = 0)
        {
            this.ExpectedSize = expectedSize;
            this.ProgressCompleted = startingSize;
            this.JobName = jobName;
        }

        #endregion

        #region Methods

        public virtual void Start()
        {
            Start(this);
            speed_lastUpdateCalculated = DateTime.Now;
            speed_progress = 0;
            Status = ProgressJobStatus.RUNNING;
            NotifyPropertyChanged("Status");
        }

        public virtual void UpdateProgress(int progress)
        {
            this.ProgressCompleted += progress;
            this.ProgressSinceLastUpdate = progress;
            this.speed_progress += progress;

            changes.Enqueue(new Tuple<DateTime, long>(DateTime.Now, progress));
            while (changes.Count > 50)
                changes.Dequeue();

            NotifyPropertyChanged("ProgressCompleted");
            NotifyPropertyChanged("ProgressRemaining");
            NotifyPropertyChanged("ProgressSinceLastUpdate");
            NotifyPropertyChanged("ProgressSpeed");
            NotifyPropertyChanged("PercentCompleted");
            MakeProgress(this, progress);
        }

        public virtual void Cancel()
        {
            this.IsCancelled = true;
            Status = ProgressJobStatus.STOPPED;
            NotifyPropertyChanged("Status");
        }

        public virtual void Finish()
        {
            Finish(this);
            Status = ProgressCompleted < ExpectedSize ? ProgressJobStatus.FAILED : ProgressJobStatus.COMPLETE;
            NotifyPropertyChanged("Status");
        }

        #endregion

        #region Private helper methods
        

        #endregion

        #region Progress events, delegates and static methods for invoking events

        public delegate void ProgressDelegate(ProgressJob p);
        public delegate void ProgressUpdatedDelegate(ProgressJob p, int progressSinceLast);

        public static event ProgressDelegate DownloadStarted;
        public static event ProgressUpdatedDelegate DownloadProgress;
        public static event ProgressDelegate DownloadFinished;

        internal static void Start(ProgressJob job)
        {
            DownloadStarted?.Invoke(job); 
        }

        internal static void MakeProgress(ProgressJob job, int progressSinceLast)
        {
            DownloadProgress?.Invoke(job, progressSinceLast);
        }

        internal static void Finish(ProgressJob job)
        {
            DownloadFinished?.Invoke(job);
        }

        #endregion

        #region PropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        #endregion
    }
}