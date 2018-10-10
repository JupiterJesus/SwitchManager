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
using log4net;
using SwitchManager.io;
using System.Threading;

namespace SwitchManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Log

        private static readonly ILog logger = LogManager.GetLogger(typeof(MainWindow));

        #endregion

        #region Fields

        private SwitchLibrary library;
        private ProgressWindow downloadWindow;
        private string metadataFile;
        private int MaxConcurrentDownloads { get; set; }
        private SemaphoreSlim DownloadThrottle { get; set; }

        #endregion 

        #region Constructor, load, close

        public MainWindow()
        {
            InitializeComponent();

            MaxConcurrentDownloads = Settings.Default.MaxConcurrentDownloads == 0 ? System.Environment.ProcessorCount : Settings.Default.MaxConcurrentDownloads;
            DownloadThrottle = new SemaphoreSlim(MaxConcurrentDownloads);

            // Initialize the window dimensions from saved settings
            if (!double.IsInfinity(Settings.Default.WindowTop) && !double.IsNaN(Settings.Default.WindowTop))
               this.Top = Settings.Default.WindowTop;
            if (!double.IsInfinity(Settings.Default.WindowLeft) && !double.IsNaN(Settings.Default.WindowLeft))
                this.Left = Settings.Default.WindowLeft;
            if (!double.IsInfinity(Settings.Default.WindowHeight) && !double.IsNaN(Settings.Default.WindowHeight))
                this.Height = Settings.Default.WindowHeight;
            if (!double.IsInfinity(Settings.Default.WindowWidth) && !double.IsNaN(Settings.Default.WindowWidth))
                this.Width = Settings.Default.WindowWidth;

            if (Settings.Default.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            EshopDownloader downloader = new EshopDownloader(Properties.Resources.TitleCertTemplate,
                                                         Properties.Resources.TitleTicketTemplate,
                                                         Settings.Default.DeviceID,
                                                         Settings.Default.Firmware,
                                                         Settings.Default.Environment,
                                                         Settings.Default.Region,
                                                         Settings.Default.ImageCache);
            downloader.UpdateClientCert(Settings.Default.ClientCertPath);
            downloader.UpdateLoginCert(Settings.Default.LoginCertPath);
            downloader.UpdateEShopCert(Settings.Default.EShopCertPath);
            downloader.ConfigureHacTool(Settings.Default.HactoolPath, Settings.Default.KeysPath);
            
            downloader.DownloadBuffer = Settings.Default.DownloadBufferSize;
            
            library = new SwitchLibrary(downloader, Settings.Default.ImageCache, Settings.Default.NSPDirectory, Settings.Default.TempDirectory);
            library.PreferredRegion = Settings.Default.Region;

            downloadWindow = new ProgressWindow(library);
            downloadWindow.Show();

            //library.LoadTitleKeysFile(Settings.Default.TitleKeysFile).Wait();

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = library.Collection;
            //


            //downloadWindow.Closing += this.Downloads_Closing;

        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.metadataFile = Settings.Default.MetadataFile + ".xml";
            FileInfo fInfo = new FileInfo(metadataFile);
            try
            {
                if (fInfo.Exists && fInfo.Length > 0)
                {
                    MakeBackup(Settings.Default.NumMetadataBackups);
                    await library.LoadMetadata(this.metadataFile);
                    Task t = Task.Run(() => library.UpdateVersions());
                }
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is CertificateDeniedException)
                    ShowError("The current certificate was denied. You can view your library but you can't make any CDN requests.");
                else
                    ShowError("Error reading library metadata file, it will be recreated on exit or when you force save it.\nIf your library is empty, make sure to update title keys and scan your library to get a fresh start.");
            }

            TextBox_Search.Text = Settings.Default.FilterText;
            CheckBox_Demos.IsChecked = Settings.Default.ShowDemos;
            CheckBox_DLC.IsChecked = Settings.Default.ShowDLC;
            CheckBox_Games.IsChecked = Settings.Default.ShowGames;
            CheckBox_Favorites.IsChecked = Settings.Default.ShowFavorites;
            CheckBox_New.IsChecked = Settings.Default.ShowNew;
            CheckBox_Owned.IsChecked = Settings.Default.ShowOwned;
            CheckBox_NotOwned.IsChecked = Settings.Default.ShowNotOwned;
            CheckBox_Preloadable.IsChecked = Settings.Default.ShowPreloadable;
            CheckBox_Preloaded.IsChecked = Settings.Default.ShowPreloaded;
            CheckBox_Hidden.IsChecked = Settings.Default.ShowHidden;
            CheckBox_Unlockable.IsChecked = Settings.Default.ShowUnlockable;

            // I wonder why I even use separate variables and a clicked handler for every checkbox
            // Why don't I grab the IsChecked values directly inside of the filter, and simply refresh
            // the datagrid for every Checked event instead of having a different handler for each?
            // Maybe later. TODO: Get rid of buttloads of redundant filter fields

            // Add a filter to the datagrid based on text filtering and checkboxes
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            Predicate<object> datagridFilter = (o =>
            {
                string filterText = TextBox_Search.Text;
                bool showDemos = CheckBox_Demos.IsChecked ?? false;
                bool showDLC = CheckBox_DLC.IsChecked ?? false;
                bool showGames = CheckBox_Games.IsChecked ?? true;
                bool showFavoritesOnly = CheckBox_Favorites.IsChecked ?? false;
                bool showNewOnly = CheckBox_New.IsChecked ?? false;
                bool showUnlockableOnly = CheckBox_Unlockable.IsChecked ?? false;
                bool showOwned = CheckBox_Owned.IsChecked ?? true;
                bool showNotOwned = CheckBox_NotOwned.IsChecked ?? true;
                bool showPreloaded = CheckBox_Preloaded.IsChecked ?? true;
                bool showPreloadable = CheckBox_Preloadable.IsChecked ?? true;
                bool showHidden = CheckBox_Hidden.IsChecked ?? false;
                SwitchCollectionItem i = o as SwitchCollectionItem;

                return  //(library.PreferredRegion.Equals(i.Region)) &&
                        ((showDemos && i.Title.IsDemo) || !i.Title.IsDemo) &&
                        ((showDLC && i.Title.IsDLC) || !i.Title.IsDLC) &&
                        ((showGames && i.Title.IsGame && !i.Title.IsDemo) || !(i.Title.IsGame && !i.Title.IsDemo)) &&
                        (!showFavoritesOnly || i.IsFavorite) &&
                        (!showUnlockableOnly || i.IsUnlockable) &&
                        (!showNewOnly || i.IsNew) &&
                        ((showOwned && i.IsOwned) || i.IsAvailable || i.IsPreloaded) &&
                        ((showNotOwned && !i.IsOwned) || i.IsOwned) &&
                        ((showPreloadable && i.IsPreloadable) || !i.IsPreloadable) &&
                        ((showPreloaded && i.IsPreloaded) || !i.IsPreloaded) &&
                        ((showHidden && i.IsHidden) || !i.IsHidden) &&
                        (string.IsNullOrWhiteSpace(filterText) || i.Title.Name.ToUpper().Contains(filterText.ToUpper()) || i.Title.TitleID.ToUpper().Contains(filterText.ToUpper()));
            });
            cv.Filter = datagridFilter;

            string sort = Settings.Default.SortColumn;
            string sDirection = Settings.Default.SortDirection;
            ListSortDirection direction = ListSortDirection.Ascending;
            if (Enum.TryParse(sDirection, out ListSortDirection d))
                direction = d;

            if (string.IsNullOrWhiteSpace(sort))
                SortGrid(1, d);
            else
                SortGrid(sort, d);
        }

        private void Window_Closed(object sender, EventArgs e)
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

            Settings.Default.FilterText = TextBox_Search.Text;
            Settings.Default.ShowDemos = CheckBox_Demos.IsChecked ?? false;
            Settings.Default.ShowDLC = CheckBox_DLC.IsChecked ?? false;
            Settings.Default.ShowGames = CheckBox_Games.IsChecked ?? true;
            Settings.Default.ShowFavorites = CheckBox_Favorites.IsChecked ?? false;
            Settings.Default.ShowNew = CheckBox_New.IsChecked ?? false;
            Settings.Default.ShowUnlockable = CheckBox_Unlockable.IsChecked ?? false;
            Settings.Default.ShowOwned = CheckBox_Owned.IsChecked ?? true;
            Settings.Default.ShowNotOwned = CheckBox_NotOwned.IsChecked ?? true;
            Settings.Default.ShowPreloadable = CheckBox_Preloadable.IsChecked ?? true;
            Settings.Default.ShowPreloaded = CheckBox_Preloaded.IsChecked ?? true;
            Settings.Default.ShowHidden = CheckBox_Hidden.IsChecked ?? false;

            var sd = DataGrid_Collection.Items.SortDescriptions.SingleOrDefault();
            if (sd != null)
            {
                Settings.Default.SortColumn = sd.PropertyName;
                Settings.Default.SortDirection = sd.Direction.ToString();
            }

            // Save all settings                
            Settings.Default.Upgrade();
            Settings.Default.Save();

            // Save library
            library.SaveMetadata(metadataFile);

            // Make sure the download window doesn't stay open
            CloseDownloadWindow();
        }

        #endregion

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

        #region Window functions & events

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

        #endregion

        #region Buttons and other controls on the datagrid

        private uint? SelectedVersion { get; set; }
        private DownloadOptions? dloption;
        public DownloadOptions? DownloadOption
        {
            get { return dloption; }
            set { dloption = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DownloadOption")); }
        }
        
        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection.SelectedValue;
            
            var title = item?.Title;
            uint version = title.BaseVersion;
            DownloadOptions o = default(DownloadOptions);

            if (title.IsUpdate)
            {
                var update = title as SwitchUpdate;
                version = update.Version;
                o = DownloadOptions.UpdateOnly;
            }
            else if (title.IsGame)
            {
                version = SelectedVersion ?? title.LatestVersion ?? title.BaseVersion;
                o = DownloadOption ?? DownloadOptions.BaseGameOnly;
            }
            else if (title.IsDLC)
            {
                version = title.LatestVersion ?? (title.LatestVersion = await library.Loader.GetLatestVersion(title)) ?? title.BaseVersion;
                o = DownloadOptions.AllDLC;
            }

            DoThreadedDownload(item, version, o);
        }

        private void DownloadUpdates_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection?.SelectedValue;
            if (item != null)
                DoThreadedDownload(item, item.Title.LatestVersion ?? item.Title.BaseVersion, DownloadOptions.UpdateOnly);
        }

        private void DownloadGameAndUpdates_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection?.SelectedValue;
            if (item != null)
                DoThreadedDownload(item, item.Title.LatestVersion ?? item.Title.BaseVersion, DownloadOptions.BaseGameAndUpdate);
        }

        private void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection?.SelectedValue;
            if (item != null)
                DoThreadedDownload(item, item.Title.LatestVersion ?? item.Title.BaseVersion, DownloadOptions.BaseGameAndUpdateAndDLC);
        }

        private void DownloadDLC_Click(object sender, RoutedEventArgs e)
        {
            SwitchCollectionItem item = (SwitchCollectionItem)DataGrid_Collection?.SelectedValue;
            if (item != null)
                DoThreadedDownload(item, item.Title.BaseVersion, DownloadOptions.AllDLC);
        }

        private void OpenRomPath_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataGrid_Collection.SelectedItem is SwitchCollectionItem item)) return;

            if (FileUtils.FileExists(item.RomPath) || FileUtils.DirectoryExists(item.RomPath))
            {
                OpenRomPath(item.RomPath);
            }
            else
            {
                item.RomPath = null;
            }
        }

        private async void RepackTitle_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataGrid_Collection.SelectedItem is SwitchCollectionItem item)) return;

            if (FileUtils.FileExists(item.RomPath))
            {
                await RepackNSP(item.RomPath);
            }
        }

        private async void RepackTitleDir_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataGrid_Collection.SelectedItem is SwitchCollectionItem item)) return;

            if (FileUtils.DirectoryExists(item.RomPath))
            {
                await RepackDir(item.RomPath);
            }
            else
            {
                string tmp = Settings.Default.TempDirectory + Path.DirectorySeparatorChar + item.TitleId;
                if (FileUtils.DirectoryExists(tmp))
                {
                    await RepackDir(tmp);
                }
            }
        }

        private void OpenRomPath(string filePath)
        {
            // combine the arguments together
            // it doesn't matter if there is a space after ','
            string argument = $"/select, \"{filePath}\"";

            System.Diagnostics.Process.Start("explorer.exe", argument);
        }

        /// <summary>
        /// Downloads a title using a separate thread (if enabled).
        /// </summary>
        /// <param name="item">Collection item to download.</param>
        /// <param name="ver">Highest version to download, if applicable (also downloads all lower versions).</param>
        /// <param name="o">Options</param>
        private async void DoThreadedDownload(SwitchCollectionItem item, uint ver, DownloadOptions o)
        {
            await DownloadThrottle.WaitAsync();

            Task t = Task.Run(async delegate
            {
                await DoDownload(item, ver, o).ConfigureAwait(false);
                DownloadThrottle.Release();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="v"></param>
        /// <param name="o"></param>
        /// <returns></returns>
        private async Task DoDownload(SwitchCollectionItem item, uint v, DownloadOptions o)
        {
            if (library.Loader.ClientCert == null)
                ShowError("There is no certificate, so don't try downloading");
            else
            {
                try
                {
                    await library.DownloadTitle(item, v, o, Settings.Default.NSPRepack, Settings.Default.VerifyDownloads);
                }
                catch (CertificateDeniedException ex)
                {
                    ShowError($"Can't download because the certificate was denied.\nTitle: {item?.TitleName ?? "Unknown"}\nMessage: {ex.Message}");
                }
                catch (CnmtMissingException ex)
                {
                    ShowError($"Can't download because we couldn't find the CNMT ID for the title.\nTitle: {item?.TitleName ?? "Unknown"}\nMessage: {ex.Message}");
                }
                catch (DownloadFailedException ex)
                {
                    ShowError($"Can't download because the download failed.\nTitle: {item?.TitleName ?? "Unknown"}\nMessage: {ex.Message}");
                }
                catch (Exception ex)
                {
                    string msg = $"Can't download because of an unknown error.\nTitle: {item?.TitleName ?? "Unknown"}\nMessage: {ex.Message}";

                    ex = ex.InnerException;
                    while (ex != null)
                    {
                        msg = msg + "\n" + ex.Message;
                        ex = ex.InnerException;
                    }
                    ShowError(msg);
                }
            }
        }

        private void RemoveTitle_Click(object sender, RoutedEventArgs e)
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

        private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            if (DataGrid_Collection.SelectedValue is SwitchCollectionItem selected)
            {
                selected.IsFavorite = !selected.IsFavorite;
            }
        }

        private void DeleteTitle_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid_Collection == null) return;

            if (DataGrid_Collection.SelectedValue is SwitchCollectionItem selected)
            {
                library.DeleteTitle(selected);
                RefreshGrid();
            }
        }

        private void VersionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;

            if (cb.SelectedValue != null)
            {
                uint v = (uint)vcon.Convert(cb.SelectedValue, typeof(uint), null, CultureInfo.InvariantCulture);
                SelectedVersion = v;
            }
        }
        VersionsConverter vcon = new VersionsConverter();

        #endregion 
        
        public event PropertyChangedEventHandler PropertyChanged;

        #region DataGrid filtering and sorting
        
        private void RefreshGrid()
        {
            if (DataGrid_Collection != null)
                CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource)?.Refresh();
        }

        private void SortGrid(int columnIndex, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            var column = DataGrid_Collection.Columns[columnIndex];
            string sort = column.SortMemberPath;
            SortGrid(sort, sortDirection);
        }

        private void SortGrid(string sort, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            DataGrid_Collection.Items.SortDescriptions.Clear();
            DataGrid_Collection.Items.SortDescriptions.Add(new SortDescription(sort, sortDirection));

            // Apply sort
            foreach (var col in DataGrid_Collection.Columns)
            {
                if (col.SortMemberPath.Equals(sort))
                    col.SortDirection = sortDirection;
                else
                    col.SortDirection = null;
            }

            // Refresh items to display sort
            RefreshGrid();
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            RefreshGrid();
        }

        private void SearchFilterChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGrid();
        }

        #endregion

        #region Menu Items

        private async void MenuItemUpdate_Click(object sender, RoutedEventArgs e)
        {
            string tempTkeysFile = "titlekeys.txt.tmp";
            string nutFile = Path.GetFullPath("nut.tmp");

            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(new Uri(Settings.Default.TitleKeysURL), tempTkeysFile);
                await client.DownloadFileTaskAsync(new Uri(Settings.Default.NutURL), nutFile);
            }

            await LoadTitleKeys(tempTkeysFile, nutFile);
            FileUtils.DeleteFile(nutFile);
            FileUtils.DeleteFile(tempTkeysFile);
        }

        private async void MenuItemLoadKeys_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".txt",
                Filter = "Title Keys Files (*.txt)|*.txt",
            };

            bool? result = dlg.ShowDialog();
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
                string temp = "titlekeys.txt.tmp";
                File.WriteAllText(temp, keys);
                await LoadTitleKeys(temp);
                FileUtils.DeleteFile(temp);
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
                this.library.Collection.Where(t => t.Title != null && (t.Size ?? 0) == 0).ToList().ForEach(async t => await UpdateSize(t));
            });
            //Parallel.ForEach(this.library.Collection, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async t => await UpdateSize(t));
        }

        private void MenuItemForceEstimateSizes_Click(object sender, RoutedEventArgs e)
        {
            // Just like regular estimate sizes, but force updates any that don't have a filename
            Task.Run(delegate
            {
                this.library.Collection.Where(t => t.Title != null && string.IsNullOrWhiteSpace(t.RomPath)).ToList().ForEach(async t => await UpdateSize(t));
            });
        }

        private void MenuItemPreloadImages_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(delegate
            {
                this.library.Collection.Where(t => t.Title != null && !t.Title.HasIcon).ToList().ForEach(async t => await library.UpdateInternalMetadata(t.Title));

            });
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

        private void CancelBulkDownloads_Click(object sender, RoutedEventArgs e)
        {
            DownloadsCancelled = true;
        }

        private async void BulkDownloadFavorites_Click(object sender, RoutedEventArgs e)
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

        private async void BulkDownloadUpdates_Click(object sender, RoutedEventArgs e)
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
                            await DownloadLimitedBySize(SwitchTitleType.Update, bytes);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private async void BulkDownloadDLC_Click(object sender, RoutedEventArgs e)
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

        private async Task DownloadAlphabetical(SwitchTitleType type)
        {
            DownloadsCancelled = false;
            var titles = GetDownloadList(type);
            var ordered = titles.OrderBy(x => x.TitleName);

            foreach (var item in ordered)
            {
                if (DownloadsCancelled)
                    break;

                var title = item?.Title;
                uint version = title.BaseVersion;

                if (title.IsUpdate)
                    version = ((SwitchUpdate)title).Version;
                else if (title.IsDLC)
                    version = title.LatestVersion ?? (title.LatestVersion = await library.Loader.GetLatestVersion(title)) ?? title.BaseVersion;

                switch (type)
                {
                    case SwitchTitleType.Game:
                        await DoDownload(item, version, DownloadOptions.BaseGameOnly);
                        break;
                    case SwitchTitleType.Update:
                        await DoDownload(item, version, DownloadOptions.UpdateOnly);
                        break;
                    case SwitchTitleType.DLC:
                        await DoDownload(item, version, DownloadOptions.AllDLC);
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
                
                if ((item.Size ?? 0) == 0)
                {
                    await UpdateSize(item);
                    if ((item.Size ?? 0) == 0)
                        break;
                }

                if (limit > 0 && accumulated + item.Size > limit)
                    break;

                var title = item?.Title;
                uint version = title.BaseVersion;

                if (title.IsUpdate)
                    version = ((SwitchUpdate)title).Version;
                else if (title.IsDLC)
                    version = title.LatestVersion ?? (title.LatestVersion = await library.Loader.GetLatestVersion(title)) ?? title.BaseVersion;

                switch (type)
                {
                    case SwitchTitleType.Game:
                        await DoDownload(item, version, DownloadOptions.BaseGameOnly);
                        break;
                    case SwitchTitleType.Update:
                        await DoDownload(item, version, DownloadOptions.UpdateOnly);
                        break;
                    case SwitchTitleType.DLC:
                        await DoDownload(item, version, DownloadOptions.AllDLC);
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

                if ((item.Size ?? 0) == 0)
                {
                    await UpdateSize(item);
                    if ((item.Size ?? 0) == 0)
                        break;
                    if (limit > 0 && item.Size > limit)
                        continue;
                }

                if (limit > 0 && item.Size > limit)
                    break;

                var title = item?.Title;
                uint version = title.BaseVersion;

                if (title.IsUpdate)
                    version = ((SwitchUpdate)title).Version;
                else if (title.IsDLC)
                    version = title.LatestVersion ?? (title.LatestVersion = await library.Loader.GetLatestVersion(title)) ?? title.BaseVersion;

                switch (type)
                {
                    case SwitchTitleType.Game:
                        await DoDownload(item, version, DownloadOptions.BaseGameOnly);
                        break;
                    case SwitchTitleType.Update:
                        await DoDownload(item, version, DownloadOptions.UpdateOnly);
                        break;
                    case SwitchTitleType.DLC:
                        await DoDownload(item, version, DownloadOptions.AllDLC);
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
                // First get the games we own, then extract their list of updates, then filter by updates not downloaded
                var games = this.library.Collection.GetGames();
                var downloaded = games.GetDownloadedTitles();
                var updates = downloaded.GetUpdates();
                var newUpdates = updates.GetTitlesNotDownloaded();
                return newUpdates;
            }
            else if (type == SwitchTitleType.DLC) // can't use GetDLC() because that gets DLC we own already, when what we want is unowned dlc for games we own
            {
                return this.library.Collection.Where(i =>
                {
                    if (i.Title.IsDLC)
                    {
                        var parent = library.GetTitleByID(((SwitchDLC)i.Title).GameID);
                        return (parent.Title.IsGame && parent.IsDownloaded);
                    }
                    return false;
                });
            }
            return null;
        }

        private async Task LoadTitleKeys(string tkeysFile, string nutFile = null)
        {
            FileInfo tempTkeys = new FileInfo(tkeysFile);

            if (tempTkeys.Exists && tempTkeys.Length >= 0)
            {
                logger.Info("Successfully downloaded new title keys file");
                var newTitles = library.UpdateTitleKeysFile(tkeysFile);
                if (nutFile != null)
                {
                    var nutTitles = library.UpdateNutFile(nutFile);
                    nutTitles.ForEach(i => newTitles.Add(i));
                }

                if (newTitles.Count > 0)
                {
                    // Update versions for the new titles!
                    Task t = Task.Run(() => library.UpdateVersions(newTitles));

                    // New titles to show you!
                    var message = string.Join(Environment.NewLine, newTitles);
                    ShowMessage(message, "New Title Keys Found!", "Awesome!");
                    RefreshGrid();
                    await t;
                }
                else
                {
                    ShowMessage("NO new titles found... :(", "Nothing new...", "Darn...");
                }
            }
            else
            {
                ShowError("Failed to download new title keys.");
                FileUtils.DeleteFile(tkeysFile);
            }
        }

        private void MenuItemImportCreds_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".pem",
                Filter = "OpenSSL Certificate (*.pem)|*.pem|PKCS12 Certificate (*.pfx)|*.pfx|All Files (*.*)|*.*"
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                // Open document 
                string newCert = dlg.FileName;
                string pfxFile = Settings.Default.ClientCertPath;

                if (string.IsNullOrWhiteSpace(newCert))
                    return;

                if (File.Exists(pfxFile))
                {
                    string backupFile = pfxFile + ".bak";
                    File.Copy(pfxFile, backupFile, true);
                }

                if (newCert.EndsWith(".pem"))
                {
                    Crypto.PemToPfx(newCert, pfxFile);
                }
                else if (newCert.EndsWith(".pfx"))
                {
                    File.Copy(newCert, pfxFile, true);
                }

                library.Loader.UpdateClientCert(pfxFile);
            }
        }

        private async void ImportGameInfo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "Game Info (JSON) (*.json)|*.json|Gmae Info (XML) (*.xml)|*.xml|All Files (*.*)|*.*"
            };

            bool? result = dlg.ShowDialog();
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

                        JObject json = JObject.Parse(File.ReadAllText(gameInfoFileName));
                        var lGames = new List<LibraryMetadataItem>();
                        foreach (var pair in json)
                        {
                            string tid = pair.Key;
                            var jsonGame = pair.Value;
                            var game = library.ParseGameInfoJson(tid, jsonGame);
                            lGames.Add(game);
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
                        foreach (var game in games)
                        {
                            game.Added = null;
                            game.IsFavorite = null;
                            game.Path = null;
                            game.Size = null;
                            game.State = null;
                            foreach (var u in game.Updates)
                            {
                                u.Added = null;
                                u.IsFavorite = null;
                                u.Path = null;
                                u.Size = null;
                                u.State = null;
                            }
                        }
                    }

                    await library.LoadMetadata(games.ToArray(), false);
                    Task t = Task.Run(() => library.UpdateVersions());
                    ShowMessage("Imported game metadata.", "Finished");
                }
            }
        }

        private void ScanLibrary_Click(object sender, RoutedEventArgs e)
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
                        ShowMessage("Library scan completed!", "Scan complete");
                    }
                }
            }
        }

        private void SelectLibraryLocation_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = Settings.Default.NSPDirectory, Description = "Choose a directory to store repacked NSP titles." };
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

        private void SelectTempLocation_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = Settings.Default.TempDirectory, Description = "Choose a directory to store downloaded title files." };
            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    if (Directory.Exists(dialog.SelectedPath))
                    {
                        Settings.Default.TempDirectory = dialog.SelectedPath;
                    }
                }
            }
        }

        private void SaveLibrary_Click(object sender, RoutedEventArgs e)
        {
            string file = Settings.Default.MetadataFile;
            if (string.IsNullOrWhiteSpace(Path.GetExtension(file)))
                file += ".xml";
            this.library.SaveMetadata(file);
        }

        private void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "Game Info JSON (*.json)|*.json|Library Metadata XML (*.xml)|*.xml",
                InitialDirectory = Path.GetDirectoryName(Settings.Default.MetadataFile),
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string exportFile = Path.GetFullPath(dlg.FileName);
                try
                {
                    library.ExportLibrary(exportFile);
                }
                catch (Exception ex)
                {
                    ShowError($"There was an unknown error while exporting the library data.\nFile: {exportFile}\nMessage: {ex.Message}");
                }
            }
        }

        #region NSP Menu

        private async void UnpackNSP_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".nsp",
                Filter = "Nintendo NSP Titles (*.nsp)|*.nsp",
                InitialDirectory = Settings.Default.NSPDirectory,
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string nspFile = dlg.FileName;
                try
                {
                    NSP nsp = await NSP.Unpack(nspFile);
                    nsp.Verify();
                }
                catch (InvalidNspException n)
                {
                    ShowError($"NSP was invalid and couldn't be unpacked\nFile: {nspFile}\nMessage: {n.Message}");
                }
                catch (BadNcaException b)
                {
                    ShowError($"NSP was unpacked but couldn't be verified.\nFile: {b.NcaFile}\nMessage: {b.Message}");
                }
            }
        }

        private async void UnpackNSPFiles_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".nsp",
                Filter = "Nintendo NSP Titles (*.nsp)|*.nsp",
                InitialDirectory = Settings.Default.NSPDirectory,
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string nspFile = dlg.FileName;
                try
                {
                    // First, unpack the NSP
                    NSP nsp = await NSP.Unpack(nspFile);
                    nsp.Verify();
                    CNMT cnmt = nsp.CNMT;
                    var item = library.GetTitleByID(cnmt.Id);
                    string titlekey = item?.TitleKey;
                    item.RequiredSystemVersion = cnmt.RequiredSystemVersion;
                    item.MasterKeyRevision = cnmt.MasterKeyRevision;

                    // Once unpacked, decrypt all the NCAs.They all go into subdirectories inside of the 
                    // NSP directory. The directory names are the NCA ID / file name without extension.
                    foreach (var nca in nsp.NcaFiles)
                    {
                        Hactool hactool = new Hactool(Settings.Default.HactoolPath, Settings.Default.KeysPath);
                        await hactool.DecryptNCA(nca, titlekey).ConfigureAwait(false);
                    }
                }
                catch (BadNcaException b)
                {
                    ShowError($"NSP was unpacked but couldn't be verified.\nFile: {b.NcaFile}\nMessage: {b.Message}");
                }
                catch (InvalidNspException n)
                {
                    ShowError($"NSP was invalid and couldn't be unpacked\nFile: {nspFile}\nMessage: {n.Message}");
                }
                catch (HactoolFailedException h)
                {
                    ShowError($"Failed to decrypt NCA with hactool\nFile: {nspFile}\nMessage: {h.Message}");
                }
            }
        }

        private async void RepackNSP_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".nsp",
                Filter = "Nintendo NSP Titles (*.nsp)|*.nsp",
                InitialDirectory = Settings.Default.NSPDirectory,
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                string nspFile = dlg.FileName;

                await RepackNSP(nspFile);
            }
        }

        private async Task RepackNSP(string nspFile, bool import)
        {
            try
            {
                // First, unpack the NSP
                NSP nsp = await NSP.Unpack(nspFile); nsp.Verify();
                CNMT cnmt = nsp.CNMT;
                var item = library.GetTitleByID(cnmt.Id);
                if (import)
                {
                    string outDir = Settings.Default.TempDirectory + Path.DirectorySeparatorChar + item.TitleId;
                    FileUtils.MoveDirectory(nsp.Directory, outDir, true);
                    await DoDownload(item, 0, DownloadOptions.BaseGameOnly);
                }
                else
                {
                    await nsp.Repack(nspFile);
                }
            }
            catch (BadNcaException b)
            {
                ShowError($"NSP was unpacked but couldn't be verified.\nFile: {b.NcaFile}\nMessage: {b.Message}");
            }
            catch (InvalidNspException n)
            {
                ShowError($"NSP was invalid and couldn't be unpacked\nFile: {nspFile}\nMessage: {n.Message}");
            }
        }

        private async void RepackDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = Settings.Default.NSPDirectory, Description = "Choose a directory to scan for titles" };
            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                await RepackDir(dialog.SelectedPath);
            }
        }

        private async Task RepackDir(string dir)
        {
            if (FileUtils.DirectoryExists(dir))
            {
                SwitchCollectionItem item = null;
                try
                {
                    // First, unpack the NSP
                    NSP nsp = NSP.FromDirectory(dir);
                    CNMT cnmt = nsp.CNMT;

                    string id = cnmt.Id;

                    uint version = cnmt.Version;

                    if (cnmt.Type == TitleType.Patch)
                    {
                        var parent = library.GetTitleByID(id);
                        item = parent.GetUpdate(version);
                    }
                    else
                    {
                        item = library.GetTitleByID(id);
                    }

                    SwitchTitle title = item?.Title;
                    title.RequiredSystemVersion = cnmt.RequiredSystemVersion;
                    title.MasterKeyRevision = cnmt.MasterKeyRevision;
                    nsp.Title = title;

                    if (title.IsTitleKeyValid)
                    {
                        await library.Loader.GenerateTitleTicket(title, cnmt, dir);
                        string nspFile = await library.DoNspRepack(title, version, nsp);

                        item.SetNspFile(nspFile);
                    }
                }
                catch (BadNcaException b)
                {
                    ShowError($"NSP was unpacked but couldn't be verified.\nTitle: {item?.TitleName ?? "Unknown"}\nFile: {b.NcaFile}\nMessage: {b.Message}");
                }
                catch (Exception b)
                {
                    ShowError($"There was an unknown error while repacking the directory. Title: {item?.TitleName ?? "Unknown"}\nMessage: {b.Message}");
                }
            }
        }

        private void VerifyDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { SelectedPath = Settings.Default.NSPDirectory, Description = "Choose a directory to scan for titles" };
            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    if (Directory.Exists(dialog.SelectedPath))
                    {
                        try
                        {
                            // First, unpack the NSP
                            NSP nsp = NSP.FromDirectory(dialog.SelectedPath);
                            nsp.Verify();
                        }
                        catch (BadNcaException b)
                        {
                            ShowError($"NSP was unpacked but couldn't be verified.\nFile: {b.NcaFile}");
                        }
                    }
                }
            }
        }

        #endregion



        #endregion

        #region Events on items in the DataGrid

        /// <summary>
        /// Navigates to the eshop for the current title
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IconButton_Click(object sender, RoutedEventArgs e)
        {
            ICollectionView cv = CollectionViewSource.GetDefaultView(DataGrid_Collection.ItemsSource);
            var item = cv.CurrentItem as SwitchCollectionItem;
            string url = item?.Title?.EshopLink;
            Process.Start(new ProcessStartInfo(url));
            e.Handled = true;
        }

        private void DataGridCell_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            var cell = (DataGridCell)sender;
            if (cell != null && cell.Column.Header.Equals("Title Name"))
            {
                if (!(DataGrid_Collection.SelectedItem is SwitchCollectionItem item)) return;

                if (!File.Exists(item.RomPath))
                {
                    item.RomPath = null;
                    return;
                }
                OpenRomPath(item.RomPath);
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var grid = sender as DataGrid;

            if (grid.CurrentItem is SwitchCollectionItem item)
            {
                if (item != null && item.Title != null)
                {
                    var title = item.Title;
                    if (title.IsGame && (!DownloadOption.HasValue || DownloadOption == DownloadOptions.AllDLC))
                    {
                        DownloadOption = DownloadOptions.BaseGameOnly;
                    }
                    else if (title.IsDLC)
                    {
                        DownloadOption = DownloadOptions.AllDLC;
                    }
                }
            }
        }

        private async void DataGrid_RowDetailsVisibilityChanged(object sender, DataGridRowDetailsEventArgs e)
        {
            var grid = sender as DataGrid;
            
            if (e?.DetailsElement?.IsVisible ?? false)
            {
                if (grid.CurrentItem is SwitchCollectionItem item)
                {
                    if (item != null && item.Title != null)
                    {
                        var title = item.Title;

                        if (title.IsGame)
                        {
                            DownloadOption = DownloadOptions.BaseGameAndUpdate;
                        }
                        else if (title.IsDLC)
                        {
                            DownloadOption = DownloadOptions.AllDLC;
                        }

                        Task t = Task.Run(async delegate
                        {
                            try
                            {
                                await library.UpdateEShopData(item);
                            }
                            catch (AggregateException ex)
                            {
                                logger.Error($"Exception getting eshop data.\nTitle: {title.Name}\nMessage: {ex.InnerException.Message}");
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Exception getting eshop data.\nTitle: {title.Name}\nMessage: {ex.Message}");
                            }
                        });

                        // If anything is null, get a new image
                        if (title.IsGame && title.IsMissingMetadata)
                        {
                            try
                            {
                                await library.UpdateInternalMetadata(title);
                            }
                            catch (HactoolFailedException ex)
                            {
                                logger.Error($"Hactool failed while getting icon file.\nTitle: {title.Name}\nMessage: {ex.Message}");
                            }
                            catch (CertificateDeniedException ex)
                            {
                                logger.Error($"Cert denied while getting icon file.\nTitle: {title.Name}\nMessage: {ex.Message}");
                            }
                            catch (DownloadFailedException ex)
                            {
                                logger.Error($"WARNING: Downloading a file failed.\nTitle: {title.Name}\nMessage: {ex.Message}");
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"WTF something failed while getting icon file.\nTitle: {title.Name}\nMessage: {ex.Message}");
                            }
                        }

                        if ((item.Size ?? 0) == 0)
                        {
                            logger.Info($"Missing size for title '{title.Name}', attempting to calculate it.");
                            try
                            {
                                await UpdateSize(item).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Exception while updating title size.\nTitle: {title.Name}\nMessage: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        #endregion

        private async Task UpdateSize(SwitchCollectionItem item)
        {
            try
            {
                string titledir = Settings.Default.TempDirectory + System.IO.Path.DirectorySeparatorChar + item?.TitleId;
                if (!Directory.Exists(titledir))
                    Directory.CreateDirectory(titledir);

                var title = item?.Title;
                uint version = title.BaseVersion;

                if (title.IsUpdate)
                    version = ((SwitchUpdate)title).Version;
                else if (title.IsDLC)
                    version = title.LatestVersion ?? (title.LatestVersion = await library.Loader.GetLatestVersion(title)) ?? title.BaseVersion;

                var result = await library.Loader.GetTitleSize(item?.Title, version, titledir);
                item.Size = result;
            }
            catch (HactoolFailedException)
            {
                logger.Error("Hactool failed while updating size.");
            }
            catch (CertificateDeniedException)
            {
                logger.Error("Cert denied while updating size.");
            }
            catch (DownloadFailedException d)
            {
                logger.Error("Downloading a file failed: " + d.Message);
            }
            catch (Exception)
            {
                logger.Error("WTF something failed while updating size.");
            }
        }

        #region MessageBox

        private void ShowError(string text)
        {
            logger.Error(text);
            Dispatcher?.InvokeOrExecute(()=>MaterialMessageBox.ShowError(text));
        }

        private void ShowMessage(string text, string title, string okButton = null, string cancel = null)
        {
            logger.Info(text);
            Dispatcher?.InvokeOrExecute(delegate
            {
                var msg = new CustomMaterialMessageBox
                {
                    MainContentControl = { Background = Brushes.White },
                    TitleBackgroundPanel = { Background = Brushes.Black },
                    BorderBrush = Brushes.Black,
                    TxtMessage = { Text = text, Foreground = Brushes.Black },
                    TxtTitle = { Text = title, Foreground = Brushes.White },
                    BtnOk = { Content = okButton ?? "OK" },
                    BtnCancel = { Content = cancel ?? "Cancel", Visibility = cancel == null ? Visibility.Collapsed : Visibility.Visible },
                };
                msg.BtnOk.Focus();
                msg.Show();
            });
        }

        #endregion
    }
}
