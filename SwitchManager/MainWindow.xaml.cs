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

namespace SwitchManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SwitchLibrary gameCollection;
        
        public MainWindow()
        {
            InitializeComponent();
            
            CDNDownloader downloader = new CDNDownloader(Settings.Default.ClientCertPath,
                                                         Settings.Default.EShopCertPath,
                                                         Settings.Default.TitleCertPath,
                                                         Settings.Default.TitleTicketPath,
                                                         Settings.Default.DeviceID, 
                                                         Settings.Default.Firmware, 
                                                         Settings.Default.Environment, 
                                                         Settings.Default.Region,
                                                         Settings.Default.ImageCache, 
                                                         Settings.Default.HactoolPath, 
                                                         Settings.Default.KeysPath);

            downloader.DownloadBuffer = Settings.Default.DownloadBufferSize;

            gameCollection = new SwitchLibrary(downloader, Settings.Default.ImageCache, Settings.Default.NSPDirectory);

            gameCollection.LoadTitleKeysFile(Settings.Default.TitleKeysFile).Wait();

            try
            {
                gameCollection.LoadMetadata(Settings.Default.MetadataFile);
            }
            catch (Exception)
            {
                MessageBox.Show("Error reading library metadata file, it will be recreated on exit or when you force save it.");
            }

            Task.Run(() => gameCollection.LoadTitleIcons(Settings.Default.ImageCache, Settings.Default.PreloadImages)).ConfigureAwait(false);

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = gameCollection.Collection;
            //

            downloader.DownloadStarted += Downloader_DownloadStarted;
            downloader.DownloadProgress += Downloader_DownloadProgress;
            downloader.DownloadFinished += Downloader_DownloadFinished;

            // Add a filter to the datagrid based on text filtering and checkboxes
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            Predicate<object> datagridFilter = (o => {
                SwitchCollectionItem i = o as SwitchCollectionItem;
                return (!this.hideDemos || (!i.Title.Name.ToUpper().EndsWith("DEMO"))) &&
                       (string.IsNullOrWhiteSpace(this.filterText) || i.Title.Name.ToUpper().Contains(filterText.ToUpper()));
            });
            cv.Filter = datagridFilter;
        }

        #region Download Progress

        private void Downloader_DownloadFinished(DownloadTask download)
        {
            Console.WriteLine($"Finished download, File: '{download.FileName}'.");
        }

        private void Downloader_DownloadProgress(DownloadTask download, int progress)
        {
            // TODO: Turn this into progress bars UI
            // TODO: Add download speed to this and estimated completion
            //System.Diagnostics.Debug.WriteLine("Bytes read: {0}", totalBytesRead);
            Console.WriteLine($"Downloaded {progress} bytes, {Miscellaneous.ToFileSize(download.Progress)}/{Miscellaneous.ToFileSize(download.ExpectedSize)} {((double)download.Progress) / download.ExpectedSize:P2} complete, File: '{download.FileName}'.");
        }

        private void Downloader_DownloadStarted(DownloadTask download)
        {
            if (download.Progress == 0)
                Console.WriteLine($"Starting download of size {Miscellaneous.ToFileSize(download.ExpectedSize)}, File: '{download.FileName}'.");
            else
                Console.WriteLine($"Resuming download at {Miscellaneous.ToFileSize(download.Progress)}/{Miscellaneous.ToFileSize(download.ExpectedSize)}, File: '{download.FileName}'.");
        }

        #endregion

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.Save();
            gameCollection.SaveMetadata(Settings.Default.MetadataFile);
        }

        #region Buttons and other controls on the datagrid

        private uint? SelectedVersion { get; set; }

        private void Button_Download_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection.SelectedValue;

            uint v = SelectedVersion ?? item.Title.Versions.First();
            
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

        #endregion 

        #region DataGrid filtering

        private string filterText = null;
        private bool hideDemos = false;

        private void TextBox_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            string filterText = searchBox.Text;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);

            this.filterText = filterText;
            cv.Refresh();
        }

        private void CheckBox_Demos_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.hideDemos = cbox.IsChecked.Value;
                cv.Refresh();
            }
        }

        #endregion

        #region Menu Items

        private async void MenuItemUpdate_Click(object sender, RoutedEventArgs e)
        {
            await DownloadTitleKeys();
        }

        private async Task DownloadTitleKeys()
        {
            string tkeysFile = System.IO.Path.GetFullPath(Settings.Default.TitleKeysFile);
            string tempTkeysFile = tkeysFile + ".tmp";

            using (var client = new WebClient())
            {
                client.DownloadFile(new Uri(Settings.Default.TitleKeysURL), tempTkeysFile);
            }

            FileInfo tempTkeys = new FileInfo(tempTkeysFile);
            FileInfo tkeys = new FileInfo(tkeysFile);

            if (tempTkeys.Exists && tempTkeys.Length >= tkeys.Length)
            {
                Console.WriteLine("Successfully downloaded new title keys file");
                var newTitles = await gameCollection.UpdateTitleKeysFile(tempTkeysFile);

                if (newTitles.Count > 0)
                {
                    // New titles to show you!
                    var message = string.Join(Environment.NewLine, newTitles);
                    MessageBox.Show(message, "New Title Keys Found!", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                File.Delete(tkeysFile);
                tempTkeys.MoveTo(tkeysFile);
            }
            else
            {
                Console.WriteLine("Failed to download new title keys file or new file was smaller than the old one");
                File.Delete(tempTkeysFile);
            }
        }

        private void MenuItemScan_Click(object sender, RoutedEventArgs e)
        {
            this.gameCollection.ScanRomsFolder(Settings.Default.NSPDirectory);
        }

        private void MenuItemSaveLibrary_Click(object sender, RoutedEventArgs e)
        {
            this.gameCollection.SaveMetadata(Settings.Default.MetadataFile);
        }

        #endregion
    }

    public class TextInputToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Always test MultiValueConverter inputs for non-null 
            // (to avoid crash bugs for views in the designer) 
            if (value is bool)
            {
                bool hasText = !(bool)value;
                if (hasText)
                    return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
