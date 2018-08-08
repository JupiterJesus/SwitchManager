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
using SwitchManager.nx.collection;
using SwitchManager.nx.cdn;
using SwitchManager.Properties;

namespace SwitchManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SwitchCollection gameCollection;
        
        public MainWindow()
        {
            InitializeComponent();
            
            CDNDownloader downloader = new CDNDownloader(Settings.Default.NXclientPath,
                                                         Settings.Default.EShopCertPath,
                                                         Settings.Default.TitleCertPath,
                                                         Settings.Default.TitleTicketPath,
                                                         Settings.Default.DeviceID, 
                                                         Settings.Default.Firmware, 
                                                         Settings.Default.Environment, 
                                                         Settings.Default.Region,
                                                         Settings.Default.ImageCache, 
                                                         Settings.Default.hactoolPath, 
                                                         Settings.Default.keysPath);

            downloader.DownloadBuffer = Settings.Default.DownloadBufferSize;

            gameCollection = new SwitchCollection(downloader, Settings.Default.ImageCache, Settings.Default.NSPDirectory);

            gameCollection.LoadTitleKeysFile(Settings.Default.TitleKeysFile).Wait();
            gameCollection.LoadMetadata(Settings.Default.MetadataFile).Wait();// ConfigureAwait(false);
            Task.Run(() => gameCollection.LoadTitleIcons(Settings.Default.ImageCache, Settings.Default.PreloadImages)).ConfigureAwait(false);

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = gameCollection.Collection;
            //

            downloader.DownloadStarted += Downloader_DownloadStarted;
            downloader.DownloadProgress += Downloader_DownloadProgress;
            downloader.DownloadFinished += Downloader_DownloadFinished;
        }

        private void Downloader_DownloadFinished(DownloadTask download)
        {
            Console.WriteLine($"Finished download, saving file to {download.FileName}.");
        }

        private void Downloader_DownloadProgress(DownloadTask download, int progress)
        {
            // TODO: Turn this into progress bars UI
            // TODO: Add download speed to this and estimated completion
            //System.Diagnostics.Debug.WriteLine("Bytes read: {0}", totalBytesRead);
            Console.WriteLine($"Downloaded {progress} bytes, {ToFileSize(download.Progress)}/{ToFileSize(download.ExpectedSize)} {((double)download.Progress) / download.ExpectedSize:P2}% complete, File:{download.FileName}.");
        }

        private void Downloader_DownloadStarted(DownloadTask download)
        {
            Console.WriteLine($"Starting download to file {download.FileName}, {ToFileSize(download.ExpectedSize)}.");
        }

        public static string ToFileSize(double value)
        {
            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value / Math.Pow(1024, i)) + " " + suffixes[i];
                }
            }

            return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
        }

        private static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
            {
                // No digits after the decimal.
                return value.ToString("0,0");
            }
            else if (value >= 10)
            {
                // One digit after the decimal.
                return value.ToString("0.0");
            }
            else
            {
                // Two digits after the decimal.
                return value.ToString("0.00");
            }
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.Save();
            gameCollection.SaveMetadata(Settings.Default.MetadataFile);
        }

        private uint? SelectedVersion { get; set; }

        private void Button_Download_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection.SelectedValue;

            uint v = SelectedVersion ?? item.title.Versions.First();
            
            Task.Run(() => gameCollection.DownloadGame(item, v, DownloadOptions.BaseGameOnly, Settings.Default.NSPRepack, false));
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            
            if (cb.SelectedValue != null)
            {
                uint v = (uint)cb.SelectedValue;
                SelectedVersion = v;
            }
        }
    }
}
