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
using SwitchManager.nx.cdn;

namespace SwitchManager.ui
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class DownloadWindow : Window
    {

        public DownloadWindow(string what)
        {
            InitializeComponent();
            Title = "Download " + what;
        }

        private void DownloadMethod_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if (radioButton == null) return;
            if (TextBlock_Description == null || !TextBlock_Description.IsInitialized) return;

            if (radioButton.IsChecked ?? false)
                TextBlock_Description.Text = radioButton.ToolTip?.ToString() ?? "";
        }

        public DownloadMethod? Method { get; private set; }
        
        private void DownloadButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (RadioButton_Alphabetical?.IsChecked ?? false)
            {
                this.Method = DownloadMethod.Alphabetical;
            }
            else if (RadioButton_Size?.IsChecked ?? false)
            {
                this.Method = DownloadMethod.BySize;
            }
            else if (RadioButton_SizeLimited?.IsChecked ?? false)
            {
                this.Method = DownloadMethod.LimitedBySize;
            }
            else if (RadioButton_DataLimited?.IsChecked ?? false)
            {
                this.Method = DownloadMethod.LimitedByData;
            }
            else 
            {
                this.Method = DownloadMethod.None;
            }
            this.DialogResult = true;

            this.Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
