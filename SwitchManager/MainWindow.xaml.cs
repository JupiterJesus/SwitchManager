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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SwitchLibrary library;
        private ProgressWindow downloadWindow;

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

            library = new SwitchLibrary(downloader, Settings.Default.ImageCache, Settings.Default.NSPDirectory);

            library.LoadTitleKeysFile(Settings.Default.TitleKeysFile).Wait();

            try
            {
                library.LoadMetadata(Settings.Default.MetadataFile);
            }
            catch (Exception)
            {
                MessageBox.Show("Error reading library metadata file, it will be recreated on exit or when you force save it.");
            }

            Task.Run(() => library.LoadTitleIcons(Settings.Default.ImageCache, Settings.Default.PreloadImages)).ConfigureAwait(false);

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = library.Collection;
            //

            downloader.DownloadStarted += Downloader_DownloadStarted;
            downloader.DownloadProgress += Downloader_DownloadProgress;
            downloader.DownloadFinished += Downloader_DownloadFinished;

            downloadWindow = new ProgressWindow(library);

            // Add a filter to the datagrid based on text filtering and checkboxes
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            Predicate<object> datagridFilter = (o =>
            {
                SwitchCollectionItem i = o as SwitchCollectionItem;
                return ((this.showDemos && (i.Title.Name.ToUpper().EndsWith("DEMO"))) || !(i.Title.Name.ToUpper().Contains("DEMO") || i.Title.Name.ToUpper().Contains("TRIAL VER"))) &&
                       ((this.showDLC && (i.Title.Name.ToUpper().StartsWith("[DLC]"))) || !i.Title.Name.StartsWith("[DLC]")) &&
                       (!this.showFavoritesOnly || (i.IsFavorite)) &&
                       ((this.showOwned && (i.State == SwitchCollectionState.Owned || i.State == SwitchCollectionState.OnSwitch)) || (i.State == SwitchCollectionState.NotOwned || i.State == SwitchCollectionState.New)) &&
                       ((this.showNotOwned && (i.State == SwitchCollectionState.NotOwned || i.State == SwitchCollectionState.New)) || (i.State == SwitchCollectionState.Owned || i.State == SwitchCollectionState.OnSwitch)) &&
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
            library.SaveMetadata(Settings.Default.MetadataFile);
            downloadWindow.Close();
        }

        #region Buttons and other controls on the datagrid

        private uint? SelectedVersion { get; set; }
        private DownloadOptions? DLOption { get; set; }

        private void Button_Download_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection.SelectedValue;

            uint v = SelectedVersion ?? item.Title.Versions.First();
            DownloadOptions o = DLOption ?? DownloadOptions.BaseGameOnly;

            Task.Run(() => library.DownloadGame(item, v, o, Settings.Default.NSPRepack, false));

            // Open the download window if it isn't showing
            if (!downloadWindow.IsVisible)
                MenuItem_ShowDownloads.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); ;
        }

        private void ComboBox_VersionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;

            if (cb.SelectedValue != null)
            {
                uint v = (uint)cb.SelectedValue;
                SelectedVersion = v;
            }
        }

        private void ComboBox_DownloadOptionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;

            if (cb.SelectedValue != null)
            {
                DownloadOptions d = (DownloadOptions)cb.SelectedValue;
                DLOption = d;
            }
        }

        #endregion 

        #region DataGrid filtering

        private string filterText = null;
        private bool showDemos = false;
        private bool showDLC = false;
        private bool showFavoritesOnly = false;
        private bool showOwned = true;
        private bool showNotOwned = true;

        private void TextBox_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            string filterText = searchBox.Text;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);

            this.filterText = filterText;
            cv.Refresh();
        }

        private void CheckBox_Demos_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showDemos = cbox.IsChecked.Value;
                cv?.Refresh();
            }
        }

        private void CheckBox_DLC_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showDLC = cbox.IsChecked.Value;
                cv?.Refresh();
            }
        }

        private void CheckBox_Favorites_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showFavoritesOnly = cbox.IsChecked.Value;
                cv?.Refresh();
            }
        }

        private void CheckBox_Owned_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showOwned = cbox.IsChecked.Value;
                cv?.Refresh();
            }
        }

        private void CheckBox_NotOwned_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showNotOwned = cbox.IsChecked.Value;
                cv?.Refresh();
            }
        }

        #endregion

        #region Menu Items

        private async void MenuItemUpdate_Click(object sender, RoutedEventArgs e)
        {
            await DownloadTitleKeys();
        }

        private void MenuItemShowDownloads_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;
            if (this.downloadWindow != null)
            {
                if (this.downloadWindow.IsVisible)
                {
                    this.downloadWindow.Hide();
                    mi.Header = "Show Downloads";
                }
                else
                {
                    this.downloadWindow.Show();
                    mi.Header = "Hide Downloads";
                }
            }
        }

        private void MenuItemImportCreds_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Not implemented, but this will provide a convenient popup window for pasting in a new device id and selecting the path to a new PEM file, and the app will handle updating the settings and converting the cert to pfx.");
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
                var newTitles = await library.UpdateTitleKeysFile(tempTkeysFile);

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
            this.library.ScanRomsFolder(Settings.Default.NSPDirectory);
            MessageBox.Show("I have most of the code written for this but it isn't completed or bug tested.");
        }

        private void MenuItemSelectLocation_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("When implemented this will simply let you choose where downloaded NSPs go. Any NSPs you already scanned into your library will remain indexed.");
        }

        private void MenuItemSaveLibrary_Click(object sender, RoutedEventArgs e)
        {
            this.library.SaveMetadata(Settings.Default.MetadataFile);
        }

        #endregion

        /// <summary>
        /// Navigates to the eshop for the current title
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            var item = cv.CurrentItem as SwitchCollectionItem;
            string url = $"https://ec.nintendo.com/apps/{item.TitleId}/{Settings.Default.Region}";
            Process.Start(new ProcessStartInfo(url));
            e.Handled = true;
        }

        private async void DataGrid_Collection_RowDetailsVisibilityChanged(object sender, DataGridRowDetailsEventArgs e)
        {
            if (e.DetailsElement.IsVisible)
            {
                ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
                var item = cv?.CurrentItem as SwitchCollectionItem;

                // If anything is null, or the blank image is used, get a new image
                if (item?.Title?.Icon == null || (item?.Title?.Icon?.Equals(SwitchLibrary.BlankImage)??true))
                {
                    await library.LoadTitleIcon(item?.Title, true);
                }
                
                if (item?.Size == 0)
                {
                    string titledir = Settings.Default.NSPDirectory + System.IO.Path.DirectorySeparatorChar + item?.TitleId;
                    if (!Directory.Exists(titledir))
                        Directory.CreateDirectory(titledir);
                    var result = await library.Loader.GetTitleSize(item?.Title, 0, titledir).ConfigureAwait(false);
                    item.Size = result;
                }
            }
        }
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
