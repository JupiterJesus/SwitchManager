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
                                                         Settings.Default.TitleCertPath,
                                                         Settings.Default.TitleTicketPath,
                                                         Settings.Default.DeviceID, 
                                                         Settings.Default.Firmware, 
                                                         Settings.Default.Environment, 
                                                         Settings.Default.ImageCache, 
                                                         Settings.Default.hactoolPath, 
                                                         Settings.Default.keysPath);

            gameCollection = new SwitchCollection(downloader, Settings.Default.ImageCache, Settings.Default.NSPDirectory);

            gameCollection.LoadTitleKeysFile(Settings.Default.TitleKeysFile);
            Task.Run(() => gameCollection.LoadTitleIcons(Settings.Default.ImageCache, Settings.Default.PreloadImages));
            gameCollection.LoadMetadata(Settings.Default.MetadataFile);

            // WHY? WHY DO I HAVE TO DO THIS TO MAKE IT WORK? DATAGRID REFUSED TO SHOW ANY DATA UNTIL I PUT THIS THING IN
            CollectionViewSource itemCollectionViewSource;
            itemCollectionViewSource = (CollectionViewSource)(FindResource("ItemCollectionViewSource"));
            itemCollectionViewSource.Source = gameCollection.Collection;
            //

            Task.Run(() => gameCollection.DownloadTitle(gameCollection.GetTitleByID("0100bc60099fe000"), 0, Settings.Default.NSPRepack, false));
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.Save();
            gameCollection.SaveMetadata(Settings.Default.MetadataFile);
        }
    }
}
