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
    public partial class TextInputWindow : Window
    {

        public TextInputWindow(string label, bool multiline = false)
        {
            InitializeComponent();
            Title = "Enter Input";
            if (multiline)
            {
                //this.Height += 200;
                TextBox_Input.Height += 200;
                TextBox_Input.VerticalAlignment = VerticalAlignment.Top;
                TextBox_Input.VerticalContentAlignment = VerticalAlignment.Top;
                TextBox_Input.TextWrapping = TextWrapping.Wrap;
                TextBox_Input.AcceptsReturn = true;
                TextBox_Input.AcceptsTab = true;
                TextBox_Input.UpdateLayout();
                Panel.UpdateLayout();
            }
            Label_Input.Text = label;
        }

        public string ResponseText { get; private set; }
        
        private void Button_OK_Clicked(object sender, RoutedEventArgs e)
        {
            this.ResponseText = this.TextBox_Input.Text;
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
