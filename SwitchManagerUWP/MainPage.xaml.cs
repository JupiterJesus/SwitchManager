using SwitchManager.nx.collection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.UI.Controls;
using SwitchManager.nx.cdn;

namespace SwitchManager
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private SwitchCollection gameCollection;

        public MainPage()
        {
            this.InitializeComponent();

            CDNDownloader downloader = new CDNDownloader("client.pfx", "0000000000000000", "5.1.0-0", "lp1");
            gameCollection = new SwitchCollection(downloader);

            gameCollection.LoadTitleKeysFile("titlekeys.txt");
            gameCollection.LoadTitleIcons("Images");
        }
    }
}
