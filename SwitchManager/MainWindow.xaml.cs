using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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
using SwitchManager.nx.cdn;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;

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

            // Initialize the window dimensions from saved settings
            this.Top = Settings.Default.WindowTop;
            this.Left = Settings.Default.WindowLeft;
            this.Height = Settings.Default.WindowHeight;
            this.Width = Settings.Default.WindowWidth;

            if (Settings.Default.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            //downloadWindow.Closing += this.Downloads_Closing;

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

            library = new SwitchLibrary(downloader, Settings.Default.ImageCache, Settings.Default.NSPDirectory, Settings.Default.TempDirectory);

            downloadWindow = new ProgressWindow(library);

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

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = library.Collection;
            //

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
                       (string.IsNullOrWhiteSpace(this.filterText) || i.Title.Name.ToUpper().Contains(filterText.ToUpper()) || i.Title.TitleID.ToUpper().Contains(filterText.ToUpper()));
            });
            cv.Filter = datagridFilter;

            SortGrid(1);
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
            // Save window dimensions!
            if (WindowState == WindowState.Maximized)
            {
                // Use the RestoreBounds as the current values will be 0, 0 and the size of the screen
                Settings.Default.WindowTop = RestoreBounds.Top;
                Settings.Default.WindowLeft = RestoreBounds.Left;
                Settings.Default.WindowHeight = RestoreBounds.Height;
                Settings.Default.WindowWidth = RestoreBounds.Width;
                Settings.Default.WindowMaximized = true;
            }
            else
            {
                Settings.Default.WindowTop = this.Top;
                Settings.Default.WindowLeft = this.Left;
                Settings.Default.WindowHeight = this.Height;
                Settings.Default.WindowWidth = this.Width;
                Settings.Default.WindowMaximized = false;
            }

            // Save all settings
            Settings.Default.Save();

            // Save library
            library.SaveMetadata(metadataFile);

            // Make sure the download window doesn't stay open
            CloseDownloadWindow();
        }

        private void CloseDownloadWindow()
        {
            if (downloadWindow != null)
                downloadWindow.Close();
        }

        private void ShowDownloadWindow()
        {
            if (downloadWindow == null)
            {
                downloadWindow = new ProgressWindow(library);
                //downloadWindow.Closing += this.Downloads_Closing;
            }

            if (downloadWindow.IsVisible)
                this.downloadWindow.Focus();
            else
                MenuItem_ShowDownloads.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
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
            if (this.IsVisible && this.IsLoaded)
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
            Task.Run(async delegate
            {
                try
                {
                    await library.DownloadTitle(item, v, o, Settings.Default.NSPRepack, false);
                }
                catch (CertificateDeniedException)
                {
                    MaterialMessageBox.ShowError("Can't download because the certificate was denied.");
                }
            });
        }

        private async Task DownloadTitle(SwitchCollectionItem item, uint v, DownloadOptions o)
        {
            await library.DownloadTitle(item, v, o, Settings.Default.NSPRepack, Settings.Default.VerifyDownloads);
        }

        private void MenuItemRemoveTitle_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            if (DataGrid_Collection.SelectedValue is SwitchCollectionItem selected)
            {
                selected.RomPath = null;
                if (selected.State == SwitchCollectionState.Owned)
                    selected.State = SwitchCollectionState.NotOwned;
                else if (selected.State == SwitchCollectionState.Downloaded)
                    selected.State = SwitchCollectionState.NoKey;
            }
        }

        private void MenuItemToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            if (DataGrid_Collection.SelectedValue is SwitchCollectionItem selected)
            {
                selected.IsFavorite = !selected.IsFavorite;
            }
        }

        private void MenuItemDeleteTitle_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            if (DataGrid_Collection.SelectedValue is SwitchCollectionItem selected)
            {
                library.DeleteTitle(selected);
            }
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

        private void SortGrid(int columnIndex, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            var column = DataGrid_Collection.Columns[columnIndex];
            DataGrid_Collection.Items.SortDescriptions.Clear();
            DataGrid_Collection.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, sortDirection));

            // Apply sort
            foreach (var col in DataGrid_Collection.Columns)
            {
                col.SortDirection = null;
            }
            column.SortDirection = sortDirection;

            // Refresh items to display sort
            DataGrid_Collection.Items.Refresh();
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
            File.Delete(tempTkeysFile);
        }

        private async void MenuItemLoadKeys_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".txt",
                Filter = "Title Keys Files (*.txt)|*.txt",
                FileName = Settings.Default.TitleKeysFile,
                InitialDirectory = new FileInfo(Settings.Default.TitleKeysFile).Directory.FullName,
            };

            Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                await LoadTitleKeys(filename);
            }
        }

        private async void MenuItemPasteKeys_Click(object sender, RoutedEventArgs e)
        {
            TextInputWindow win = new TextInputWindow("Paste title keys in the standard format.\n\nTITLE ID (16 hex chars)|TITLE KEY (32 hex chars)|TITLE NAME\n\nIf the title key is missing, you can still download the title, but you need a key to unlock it. A missing name will be automatically replaced with the correct name by the app.", true);
            win.Width = 600;
            bool? result = win.ShowDialog();
            if (result ?? false)
            {
                string keys = win.ResponseText;
                string temp = Settings.Default.TitleKeysFile + ".tmp";
                File.WriteAllText(temp, keys);
                await LoadTitleKeys(temp);
                File.Delete(temp);
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
                this.library.Collection.Where(t => t.Title != null && t.Title.Type == SwitchTitleType.Game && (t.Size ?? 0) == 0).ToList().ForEach(async t => await UpdateSize(t));
            });
            //Parallel.ForEach(this.library.Collection, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async t => await UpdateSize(t).ConfigureAwait(false));
        }

        private void MenuItemForceEstimateSizes_Click(object sender, RoutedEventArgs e)
        {
            // Just like regular estimate sizes, but force updates any that don't have a filename
            Task.Run(delegate
            {
                this.library.Collection.Where(t => t.Title != null && t.Title.Type == SwitchTitleType.Game && string.IsNullOrWhiteSpace(t.RomPath)).ToList().ForEach(async t => await UpdateSize(t));
            });
        }

        private void MenuItemPreloadImages_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => library.LoadTitleIcons(Settings.Default.ImageCache)).ConfigureAwait(false);
        }

        private void MenuItemShowDownloads_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = (MenuItem)sender;

            if (this.downloadWindow == null)
                ShowDownloadWindow();
            else
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

        private bool DownloadsCancelled { get; set; } = false;

        private void MenuItemCancelDownloads_Click(object sender, RoutedEventArgs e)
        {
            DownloadsCancelled = true;
        }

        private async void MenuItemDownloadFavorites_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new DownloadWindow("Favorite Games");
                bool s = win.ShowDialog() ?? false;
                if (s)
                {
                    var method = win.Method ?? DownloadMethod.None;
                    switch (method)
                    {
                        case DownloadMethod.Alphabetical:
                            await DownloadAlphabetical(SwitchTitleType.Game);
                            break;
                        case DownloadMethod.BySize:
                            await DownloadLimitedBySize(SwitchTitleType.Game);
                            break;
                        case DownloadMethod.LimitedByData:

                            var maxDataWin = new TextInputWindow("Please enter the total amount of data you are willing to download. Favorites will be downloaded until the next download would exceed this number, or if the size is unknown, then stops.\n\nPlease enter your answer like the following, with a number followed by a unit size: 1000 bytes, 6000 MB, 100 GB, 1.36 TB.");
                            s = maxDataWin.ShowDialog() ?? false;
                            if (s)
                            {
                                string maxSize = maxDataWin.ResponseText;
                                long bytes = Miscellaneous.FromFileSize(maxSize);
                                await DownloadLimitedByData(SwitchTitleType.Game, bytes);
                            }
                            break;
                        case DownloadMethod.LimitedBySize:

                            var maxSizeWin = new TextInputWindow("Please enter a limit to the biggest game you want to download. If the next title is larger than this amount, or if the size is unknown, downloading will stop.\n\nPlease enter your answer like the following, with a number followed by a unit size: 1000 bytes, 6000 MB, 100 GB, 1.36 TB.");
                            s = maxSizeWin.ShowDialog() ?? false;
                            if (s)
                            {
                                string maxSize = maxSizeWin.ResponseText;
                                long bytes = Miscellaneous.FromFileSize(maxSize);
                                await DownloadLimitedBySize(SwitchTitleType.Game, bytes);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (CertificateDeniedException)
            {
                MaterialMessageBox.ShowError("Can't download because the certificate was denied.");
            }
        }

        private async void MenuItemDownloadUpdates_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new DownloadWindow("Updates");
                bool s = win.ShowDialog() ?? false;
                if (s)
                {
                    var method = win.Method ?? DownloadMethod.None;
                    switch (method)
                    {
                        case DownloadMethod.Alphabetical:
                            await DownloadAlphabetical(SwitchTitleType.Update);
                            break;
                        case DownloadMethod.BySize:
                            await DownloadLimitedBySize(SwitchTitleType.Update);
                            break;
                        case DownloadMethod.LimitedByData:

                            var maxDataWin = new TextInputWindow("Please enter the total amount of data you are willing to download. Updates will be downloaded until the next download would exceed this number, or if the size is unknown, then stops.\n\nPlease enter your answer like the following, with a number followed by a unit size: 1000 bytes, 6000 MB, 100 GB, 1.36 TB.");
                            s = maxDataWin.ShowDialog() ?? false;
                            if (s)
                            {
                                string maxSize = maxDataWin.ResponseText;
                                long bytes = Miscellaneous.FromFileSize(maxSize);
                                await DownloadLimitedByData(SwitchTitleType.Update, bytes);
                            }
                            break;
                        case DownloadMethod.LimitedBySize:

                            var maxSizeWin = new TextInputWindow("Please enter a limit to the biggest update you want to download. If the next update is larger than this amount, or if the size is unknown,  downloading will stop.\n\nPlease enter your answer like the following, with a number followed by a unit size: 1000 bytes, 6000 MB, 100 GB, 1.36 TB.");
                            s = maxSizeWin.ShowDialog() ?? false;
                            if (s)
                            {
                                string maxSize = maxSizeWin.ResponseText;
                                long bytes = Miscellaneous.FromFileSize(maxSize);
                                await DownloadLimitedBySize(SwitchTitleType.DLC, bytes);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (CertificateDeniedException)
            {
                MaterialMessageBox.ShowError("Can't download because the certificate was denied.");
            }
        }

        private async void MenuItemDownloadDLC_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new DownloadWindow("DLC");
                bool s = win.ShowDialog() ?? false;
                if (s)
                {
                    var method = win.Method ?? DownloadMethod.None;
                    switch (method)
                    {
                        case DownloadMethod.Alphabetical:
                            await DownloadAlphabetical(SwitchTitleType.DLC);
                            break;
                        case DownloadMethod.BySize:
                            await DownloadLimitedBySize(SwitchTitleType.DLC);
                            break;
                        case DownloadMethod.LimitedByData:

                            var maxDataWin = new TextInputWindow("Please enter the total amount of data you are willing to download. DLC will be downloaded until the next download would exceed this number,  or if the size is unknown, then stops.\n\nPlease enter your answer like the following, with a number followed by a unit size: 1000 bytes, 6000 MB, 100 GB, 1.36 TB.");
                            s = maxDataWin.ShowDialog() ?? false;
                            if (s)
                            {
                                string maxSize = maxDataWin.ResponseText;
                                long bytes = Miscellaneous.FromFileSize(maxSize);
                                await DownloadLimitedByData(SwitchTitleType.DLC, bytes);
                            }
                            break;
                        case DownloadMethod.LimitedBySize:

                            var maxSizeWin = new TextInputWindow("Please enter a limit to the biggest DLC you want to download. If the next DLC is larger than this amount, or if the size is unknown, downloading will stop.\n\nPlease enter your answer like the following, with a number followed by a unit size: 1000 bytes, 6000 MB, 100 GB, 1.36 TB.");
                            s = maxSizeWin.ShowDialog() ?? false;
                            if (s)
                            {
                                string maxSize = maxSizeWin.ResponseText;
                                long bytes = Miscellaneous.FromFileSize(maxSize);
                                await DownloadLimitedBySize(SwitchTitleType.DLC, bytes);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (CertificateDeniedException)
            {
                MaterialMessageBox.ShowError("Can't download because the certificate was denied.");
            }
        }

        private async Task DownloadAlphabetical(SwitchTitleType type)
        {
            DownloadsCancelled = false;
            var titles = GetDownloadList(type);
            var ordered = titles.OrderBy(x => x.TitleName);

            foreach (var item in ordered)
            {
                if (DownloadsCancelled)
                    break;

                switch (type)
                {
                    case SwitchTitleType.Game:
                        await DownloadTitle(item, item.Title.BaseVersion, DownloadOptions.BaseGameOnly).ConfigureAwait(false);
                        break;
                    case SwitchTitleType.Update:
                        await DownloadTitle(item, item.Title.LatestVersion, DownloadOptions.UpdateOnly).ConfigureAwait(false);
                        break;
                    case SwitchTitleType.DLC:
                        await DownloadTitle(item, item.Title.BaseVersion, DownloadOptions.AllDLC).ConfigureAwait(false);
                        break;
                }
            }
        }

        private async Task DownloadLimitedByData(SwitchTitleType type, long limit = 0)
        {
            DownloadsCancelled = false;
            var titles = GetDownloadList(type);
            var ordered = titles.OrderBy(x => x.Size);
            long accumulated = 0;

            foreach (var item in ordered)
            {
                if (DownloadsCancelled)
                    break;

                if (!item.Size.HasValue)
                    break;

                if (limit > 0 && accumulated + item.Size > limit)
                    break;

                switch (type)
                {
                    case SwitchTitleType.Game:
                        await DownloadTitle(item, item.Title.BaseVersion, DownloadOptions.BaseGameOnly).ConfigureAwait(false);
                        break;
                    case SwitchTitleType.Update:
                        await DownloadTitle(item, item.Title.LatestVersion, DownloadOptions.UpdateOnly).ConfigureAwait(false);
                        break;
                    case SwitchTitleType.DLC:
                        await DownloadTitle(item, item.Title.BaseVersion, DownloadOptions.AllDLC).ConfigureAwait(false);
                        break;
                }

                accumulated += item.Size.Value;
            }
        }

        private async Task DownloadLimitedBySize(SwitchTitleType type, long limit = 0)
        {
            DownloadsCancelled = false;
            var titles = GetDownloadList(type);
            var ordered = titles.OrderBy(x => x.Size);

            foreach (var item in ordered)
            {
                if (DownloadsCancelled)
                    break;

                if (!item.Size.HasValue)
                    break;

                if (limit > 0 && item.Size > limit)
                    break;


                switch (type)
                {
                    case SwitchTitleType.Game:
                        await DownloadTitle(item, item.Title.BaseVersion, DownloadOptions.BaseGameOnly).ConfigureAwait(false);
                        break;
                    case SwitchTitleType.Update:
                        await DownloadTitle(item, item.Title.LatestVersion, DownloadOptions.UpdateOnly).ConfigureAwait(false);
                        break;
                    case SwitchTitleType.DLC:
                        await DownloadTitle(item, item.Title.BaseVersion, DownloadOptions.AllDLC).ConfigureAwait(false);
                        break;
                }
            }
        }

        private IEnumerable<SwitchCollectionItem> GetDownloadList(SwitchTitleType type)
        {
            if (type == SwitchTitleType.Game) // Says game here but actually gets DLC favorites too
            {
                return this.library.Collection.GetFavoriteTitles().GetTitlesNotDownloaded();
            }
            else if (type == SwitchTitleType.Update)
            {
                return this.library.Collection.GetDownloadedTitles().GetUpdates();
            }
            else if (type == SwitchTitleType.DLC) // can't use GetDLC() because that gets DLC we own already, when what we want is unowned dlc for games we own
            {
                return this.library.Collection.Where(i =>
                {
                    if (i.Title is SwitchDLC && i.Title.IsDLC)
                    {
                        var parent = library.GetBaseGameByID(i.TitleId);
                        return (i.Title is SwitchGame && i.Title.IsGame && i.IsDownloaded);
                    }
                    return false;
                });
            }
            return null;
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
                File.Copy(pfxFile, backupFile, true);

            }
        }

        private async void MenuItemImportGameInfo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "Game Info (JSON) (*.json)|*.json|Gmae Info (XML) (*.xml)|*.xml|All Files (*.*)|*.*"
            };

                Nullable<bool> result = dlg.ShowDialog();
            if (result == true)
            {
                // Open document 
                string gameInfoFileName = dlg.FileName;
                string ext = System.IO.Path.GetExtension(gameInfoFileName)?.ToLower() ?? null;
                
                if (gameInfoFileName != null && ext != null && File.Exists(gameInfoFileName))
                {
                    IEnumerable<LibraryMetadataItem> games = null;

                    if (".json".Equals(ext))
                    {
                        var lGames = new List<LibraryMetadataItem>();

                        JObject json = JObject.Parse(File.ReadAllText(gameInfoFileName));
                        foreach (var pair in json)
                        {
                            string tid = pair.Key;
                            var jsonGame = pair.Value;
                            if (SwitchTitle.IsBaseGameID(tid) && jsonGame.HasValues)
                            {
                                LibraryMetadataItem game = new LibraryMetadataItem();

                                game.TitleID = jsonGame?.Value<string>("titleid");
                                game.Name = jsonGame?.Value<string>("title");
                                game.Publisher = jsonGame?.Value<string>("publisher");
                                game.Developer = jsonGame?.Value<string>("developer");
                                game.Description = jsonGame?.Value<string>("description");
                                game.Intro = jsonGame?.Value<string>("intro");
                                game.Category = jsonGame?.Value<string>("category");
                                game.BoxArtUrl = jsonGame?.Value<string>("front_box_art");
                                game.Rating = jsonGame?.Value<string>("rating");
                                game.NumPlayers = jsonGame?.Value<string>("number_of_players");
                                game.NsuId = jsonGame?.Value<string>("nsuid");
                                game.Code = jsonGame?.Value<string>("game_code");
                                game.RatingContent = jsonGame?.Value<string>("content");
                                game.Price = jsonGame?.Value<string>("US_price");

                                string s = jsonGame?.Value<string>("dlc");
                                if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out bool hasDlc))
                                    game.HasDLC = hasDlc;
                                else
                                    game.HasDLC = false;

                                s = jsonGame?.Value<string>("amiibo_compatibility");
                                if (!string.IsNullOrWhiteSpace(s) && bool.TryParse(s, out bool hasA))
                                    game.HasAmiibo = hasA;
                                else
                                    game.HasAmiibo = false;

                                string sSize = jsonGame?.Value<string>("Game_size");
                                if (long.TryParse(sSize, out long size))
                                    game.Size = size;

                                if (DateTime.TryParseExact(jsonGame?.Value<string>("release_date_iso"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime rDate))
                                    game.ReleaseDate = rDate;

                                lGames.Add(game);
                            }
                        }
                        games = lGames;
                    }
                    else if (".xml".Equals(ext))
                    {
                        XmlSerializer xml = new XmlSerializer(typeof(LibraryMetadata));
                        LibraryMetadata lib;

                        using (FileStream fs = File.OpenRead(gameInfoFileName))
                            lib = xml.Deserialize(fs) as LibraryMetadata;

                        games = lib.Items;
                    }

                    await library.LoadMetadata(games).ConfigureAwait(false);
                }
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
            if (e?.DetailsElement?.IsVisible ?? false)
            {
                ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
                var item = cv?.CurrentItem as SwitchCollectionItem;
                if (item != null)
                {

                    // If anything is null, or the blank image is used, get a new image
                    if (item.Title?.Icon == null || (item.Title?.Icon?.Equals(SwitchLibrary.BlankImage) ?? true))
                    {
                        try
                        {
                            await library.LoadTitleIcon(item?.Title, true);
                        }
                        catch (HactoolFailedException)
                        {
                            Console.WriteLine("WARNING: Hactool failed while getting icon file.");
                        }
                        catch (CertificateDeniedException)
                        {
                            Console.WriteLine("WARNING: Cert denied while getting icon file.");
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("WARNING: WTF something failed while getting icon file.");
                        }
                    }

                    if (item != null && ((item.Size ?? 0) == 0))
                    {
                        try
                        {
                            await UpdateSize(item);
                        }
                        catch (HactoolFailedException)
                        {
                            Console.WriteLine("WARNING: Hactool failed while updating size.");
                        }
                        catch (CertificateDeniedException)
                        {
                            Console.WriteLine("WARNING: Cert denied while updating size.");
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("WARNING: WTF something failed while updating size.");
                        }
                    }
                }
            }
        }

        private async Task UpdateSize(SwitchCollectionItem item)
        {
            string titledir = Settings.Default.TempDirectory + System.IO.Path.DirectorySeparatorChar + item?.TitleId;
            if (!Directory.Exists(titledir))
                Directory.CreateDirectory(titledir);
            var result = await library.Loader.GetTitleSize(item?.Title, 0, titledir).ConfigureAwait(false);
            item.Size = result;
        }
    }
}
