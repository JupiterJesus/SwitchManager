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
using SwitchManager.nx.net;

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

            CDNDownloader downloader = new CDNDownloader("client.pfx", "0000000000000000", "5.1.0-0", "lp1");
            gameCollection = new SwitchCollection(downloader);
            //            gameCollection.AddGame("Test Game 1", "titleid1", "titlekey1", SwitchCollectionState.NOT_OWNED, true);
            //            gameCollection.AddGame("Test Game 2", "titleid2", "titlekey2", SwitchCollectionState.OWNED, false);
            //            gameCollection.AddGame("Test Game 3", "titleid3", "titlekey3", SwitchCollectionState.DOWNLOADED);
            //            gameCollection.AddGame("Test Game 4", "titleid4", "titlekey4", SwitchCollectionState.ON_SWITCH);
            //            gameCollection.AddGame("Test Game 5", "titleid5", "titlekey5", true);

            gameCollection.LoadTitleKeysFile("titlekeys.txt");
            gameCollection.LoadTitleIcons("Images");
        }
    }
}
