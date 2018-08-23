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
using SwitchManager.nx.system;
using SwitchManager.Properties;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using SwitchManager.util;
using System.Diagnostics;
using BespokeFusion;
using SwitchManager.ui;
using System.Threading;
using SwitchManager.nx.cdn;

namespace SwitchManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SwitchLibrary library;
        private ProgressWindow downloadWindow;
        private string metadataFile;

        public MainWindow()
        {
            InitializeComponent();

            EshopDownloader downloader = new EshopDownloader(Settings.Default.ClientCertPath,
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

            //library.LoadTitleKeysFile(Settings.Default.TitleKeysFile).Wait();

            this.metadataFile = Settings.Default.MetadataFile + ".xml";
            try
            {
                MakeBackup(Settings.Default.NumMetadataBackups);
                library.LoadMetadata(this.metadataFile).Wait();
            }
            catch (AggregateException e)
            {
                if (e.InnerException is CertificateDeniedException)
                    MaterialMessageBox.ShowError("The current certificate was denied. You can view your library but you can't make any CDN requests.");
                else
                    MaterialMessageBox.ShowError("Error reading library metadata file, it will be recreated on exit or when you force save it.\nIf your library is empty, make sure to update title keys and scan your library to get a fresh start.");
            }

            Task.Run(() => library.LoadTitleIcons(Settings.Default.ImageCache, Settings.Default.PreloadImages)).ConfigureAwait(false);

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = library.Collection;
            //

            downloadWindow = new ProgressWindow(library);
            downloadWindow.Closing += this.Downloads_Closing;

            // Add a filter to the datagrid based on text filtering and checkboxes
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            Predicate<object> datagridFilter = (o =>
            {
                SwitchCollectionItem i = o as SwitchCollectionItem;
                return ((this.showDemos && i.Title.IsDemo) || !i.Title.IsDemo) &&
                       ((this.showDLC && i.Title.IsDLC) || !i.Title.IsDLC) &&
                       (!this.showFavoritesOnly || (i.IsFavorite)) &&
                       (!this.showNewOnly || (i.State == SwitchCollectionState.New)) &&
                       ((this.showOwned && (i.State == SwitchCollectionState.Owned || i.State == SwitchCollectionState.OnSwitch)) || (!(i.State == SwitchCollectionState.Owned || i.State == SwitchCollectionState.OnSwitch))) &&
                       ((this.showNotOwned && (!(i.State == SwitchCollectionState.Owned || i.State == SwitchCollectionState.OnSwitch))) || (i.State == SwitchCollectionState.Owned || i.State == SwitchCollectionState.OnSwitch)) &&
                       ((this.showHidden && (i.State == SwitchCollectionState.Hidden)) || (i.State != SwitchCollectionState.Hidden)) &&
                       (string.IsNullOrWhiteSpace(this.filterText) || i.Title.Name.ToUpper().Contains(filterText.ToUpper()));
            });
            cv.Filter = datagridFilter;
        }

        /// <summary>
        /// Make backups of the library file, according to the number of backups that should be kept.
        /// The format of the backup is simply {libraryfile.xml}.0, {libraryfile.xml}.1, ..., {libraryfile.xml}.n,
        /// where n increments every time a new backup is made. The oldest backup is deleted if the max number
        /// of backups has been reached.
        /// </summary>
        /// <param name="metadataFile"></param>
        /// <param name="nBackups"></param>
        private void MakeBackup(int nBackups)
        {
            var libFile = new FileInfo(metadataFile);
            var parent = libFile.Directory;
            var backups = parent.EnumerateFiles(metadataFile + ".*").Where(f => !metadataFile.Equals(f.Name)).OrderBy((f) => f.CreationTime).ToArray();

            int nextIndex = backups.Length;
            if (nextIndex >= nBackups)
            {
                string latestBackup = backups.Last().Name;
                string sIndex = latestBackup.Replace(metadataFile + ".", string.Empty);
                if (int.TryParse(sIndex, out int index))
                {
                    nextIndex = index + 1;
                    backups.First().Delete();
                }
            }
            File.Copy(metadataFile, $"{metadataFile}.{nextIndex}");
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.Save();
            library.SaveMetadata(metadataFile);
            downloadWindow.Close();
        }

        /// <summary>
        /// Replaces the window close with a hide, but only if the main app is still open. Otherwise, it is impossible
        /// to close since the download window always cancels the close and it is hidden. If I don't cancel the close,
        /// the window is closed and disposed and a new one must be created every time, losing your download history.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Downloads_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.IsVisible)
            {
                //    e.Cancel = true;
                //    downloadWindow.Hide();
                //    MenuItem_ShowDownloads.Header = "Show Downloads";
            }
        }

        #region Buttons and other controls on the datagrid

        private uint? SelectedVersion { get; set; }
        private DownloadOptions? DLOption { get; set; }

        private void Button_Download_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection.SelectedValue;

            uint v = SelectedVersion ?? item.Title.BaseVersion;
            DownloadOptions o = DLOption ?? DownloadOptions.BaseGameOnly;

            // Okay so the below got way more complicated than it used to be. I wanted to catch an exception in case
            // the cert is denied. I had to put that in the threaded task. Since I didn't want the download window to open
            // or focus unless the download started, I think had to put that in there right after
            Task.Run(delegate
            {
                try
                {
                    library.DownloadGame(item, v, o, Settings.Default.NSPRepack, false);
                }
                catch (CertificateDeniedException)
                {
                    MaterialMessageBox.ShowError("Can't download because the certificate was denied.");
                }
            });
            // Open the download window if it isn't showing
            if (downloadWindow.IsVisible)
                this.downloadWindow.Focus();
            else
                MenuItem_ShowDownloads.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
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
        private bool showNewOnly = false;
        private bool showOwned = true;
        private bool showNotOwned = true;
        private bool showHidden = false;

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

        private void CheckBox_New_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showNewOnly = cbox.IsChecked.Value;
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

        private void CheckBox_Hidden_Checked(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            CheckBox cbox = (CheckBox)sender;
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection?.ItemsSource);
            if (cbox.IsChecked.HasValue)
            {
                this.showHidden = cbox.IsChecked.Value;
                cv?.Refresh();
            }
        }

        #endregion

        #region Menu Items

        private async void MenuItemUpdate_Click(object sender, RoutedEventArgs e)
        {
            string tkeysFile = System.IO.Path.GetFullPath(Settings.Default.TitleKeysFile);
            string tempTkeysFile = tkeysFile + ".tmp";

            using (var client = new WebClient())
            {
                client.DownloadFile(new Uri(Settings.Default.TitleKeysURL), tempTkeysFile);
            }

            await LoadTitleKeys(tempTkeysFile);
        }

        private async void MenuItemLoadKeys_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Title Keys Files (*.txt)|*.txt";

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                await LoadTitleKeys(filename);
            }
        }

        /// <summary>
        /// Estimates sizes of all games (no updates, demos or dlc) that don't already have a size.
        /// It is buggy though - it sometimes fails due to some threading issue
        /// // Maybe if instead of spawning a thread for each, I spawn one thread for all of them but do them all in a row?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItemEstimateSizes_Click(object sender, RoutedEventArgs e)
        {
            // Here I severely limit what I even try to get sizes for, because I've found that some types will crash the shit out of this
            // Demos and DLC, for example, will fail hard
            // But those same demos and DLC sometimes work fine when I individually click on them to update the size

            Task.Run(delegate
            {
                this.library.Collection.FindAll(t => t.Title != null && t.Title.Type == SwitchTitleType.Game && (t.Size ?? 0) == 0).ForEach(async t => await UpdateSize(t));
            });
            //Parallel.ForEach(this.library.Collection, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async t => await UpdateSize(t).ConfigureAwait(false));
        }

        private void MenuItemForceEstimateSizes_Click(object sender, RoutedEventArgs e)
        {
            // Just like regular estimate sizes, but force updates any that don't have a filename
            Task.Run(delegate
            {
                this.library.Collection.FindAll(t => t.Title != null && t.Title.Type == SwitchTitleType.Game && string.IsNullOrWhiteSpace(t.RomPath)).ForEach(async t => await UpdateSize(t));
            });
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
                    this.downloadWindow.Focus();
                    mi.Header = "Hide Downloads";
                }
            }
        }

        private void MenuItemDownloadAlpha_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuItemDownloadSize_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuItemDownloadSmallest_Click(object sender, RoutedEventArgs e)
        {
        }

        private void MenuItemDownloadLimited_Click(object sender, RoutedEventArgs e)
        {
        }

        private async Task LoadTitleKeys(string tkeysFile)
        {
            FileInfo tempTkeys = new FileInfo(tkeysFile);

            if (tempTkeys.Exists && tempTkeys.Length >= 0)
            {
                Console.WriteLine("Successfully downloaded new title keys file");
                var newTitles = await library.UpdateTitleKeysFile(tkeysFile);


                if (newTitles.Count > 0)
                {
                    // New titles to show you!
                    var message = string.Join(Environment.NewLine, newTitles);
                    ShowMessage(message, "New Title Keys Found!", "Awesome!");
                }
                else
                {
                    ShowMessage("NO new titles found... :(", "Nothing new...", "Darn...");
                }
                File.Delete(tkeysFile);
            }
            else
            {
                MaterialMessageBox.ShowError("Failed to download new title keys.");
                File.Delete(tkeysFile);
            }
        }

        private void MenuItemImportCreds_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".pem",
                Filter = "OpenSSL Certificate (*.pem)|*.pem|PKCS12 Certificate (*.pfx)|*.pfx|All Files (*.*)|*.*"
            };

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                // Open document 
                string newCert = dlg.FileName;
                string pfxFile = Settings.Default.ClientCertPath;
                string backupFile = pfxFile + ".bak";
                File.Copy(pfxFile, backupFile);

                if (".PFX".Equals(System.IO.Path.GetExtension(newCert)))
                {
                    File.Copy(newCert, pfxFile);
                }
                else if (".PEM".Equals(System.IO.Path.GetExtension(newCert)))
                {
                    Crypto.PemToPfx(newCert, pfxFile);
                }

                library.Loader.UpdateClientCert(pfxFile);
                ShowMessage(@"Your certificate located at {pemFile} was successfully imported. Just in case something didn't work out, I made a backup of your old certificate at {backupFile}. Your certificate is located at {pfxFile}.", "Certificate imported!", "Thanks!");
            }

        }

        private void ShowMessage(string text, string title, string okButton = null, string cancel = null)
        {
            var msg = new CustomMaterialMessageBox
            {
                MainContentControl = { Background = Brushes.White },
                TitleBackgroundPanel = { Background = Brushes.Black },
                BorderBrush = Brushes.Black,
                TxtMessage = { Text = text, Foreground = Brushes.Black },
                TxtTitle = { Text = title, Foreground = Brushes.White },
                BtnOk = { Content = okButton ?? "OK" },
                BtnCancel = { Content = cancel ?? "Cancel", Visibility = cancel == null ? Visibility.Collapsed : Visibility.Visible},
            };
            msg.BtnOk.Focus();
            msg.Show();
        }

        private void MenuItemScan_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = Settings.Default.NSPDirectory, Description = "Choose a directory to scan for titles" };
            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    if (Directory.Exists(dialog.SelectedPath))
                    {
                        this.library.ScanRomsFolder(dialog.SelectedPath);
                        MaterialMessageBox.Show("Library scan completed!", "Scan complete");
                    }
                }
            }
        }

        private void MenuItemSelectLocation_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = Settings.Default.NSPDirectory, Description = "Choose a directory to store downloaded titles" };
            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    if (Directory.Exists(dialog.SelectedPath))
                    {
                        Settings.Default.NSPDirectory = dialog.SelectedPath;
                    }
                }
            }
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
                if (item != null)
                {

                    // If anything is null, or the blank image is used, get a new image
                    if (item.Title?.Icon == null || (item.Title?.Icon?.Equals(SwitchLibrary.BlankImage) ?? true))
                    {
                        await library.LoadTitleIcon(item?.Title, true);
                    }

                    if ((item.Size ?? 0) == 0)
                    {
                        await UpdateSize(item);
                    }
                }
            }
        }

        private async Task UpdateSize(SwitchCollectionItem item)
        {
            string titledir = Settings.Default.NSPDirectory + System.IO.Path.DirectorySeparatorChar + item?.TitleId;
            if (!Directory.Exists(titledir))
                Directory.CreateDirectory(titledir);
            var result = await library.Loader.GetTitleSize(item?.Title, 0, titledir).ConfigureAwait(false);
            item.Size = result;
        }
    }
}
