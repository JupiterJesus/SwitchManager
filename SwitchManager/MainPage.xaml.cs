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

            gameCollection = new SwitchCollection();
//            gameCollection.AddGame("Test Game 1", "titleid1", "titlekey1", SwitchCollectionState.NOT_OWNED, true);
//            gameCollection.AddGame("Test Game 2", "titleid2", "titlekey2", SwitchCollectionState.OWNED, false);
//            gameCollection.AddGame("Test Game 3", "titleid3", "titlekey3", SwitchCollectionState.DOWNLOADED);
//            gameCollection.AddGame("Test Game 4", "titleid4", "titlekey4", SwitchCollectionState.ON_SWITCH);
//            gameCollection.AddGame("Test Game 5", "titleid5", "titlekey5", true);

            gameCollection.LoadTitleKeysFile("titlekeys.txt");
        }
    }
}
