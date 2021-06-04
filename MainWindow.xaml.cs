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
using RTKModule;

namespace UserTool
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Frequency.ItemsSource = Array.ConvertAll<CH, int>(Wifi.chDic.Keys.ToArray(), delegate (CH ch) { return (int)ch; });
            //Bandwidth.ItemsSource = Wifi.bwDic.Values;
            //RateID.ItemsSource = Wifi.rateIdDic.Values;
            //Antenna.ItemsSource = Wifi.antPathDic.Values.TakeWhile((value) => !value.Equals(Wifi.antPathDic[ANT_PATH.PATH_AB]));

            //// default
            //Frequency.SelectedValue = (int)CH.CH14;
            //Bandwidth.SelectedValue = Wifi.bwDic[BW.B_20MHZ];
            //RateID.SelectedValue = Wifi.rateIdDic[RATE_ID.R_54M];
            //Antenna.SelectedValue = Wifi.antPathDic[ANT_PATH.PATH_B];
        }
    }
}
