using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Unity;
using UserTool.ViewModel;

namespace UserTool
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        //protected override void OnStartup(StartupEventArgs e)
        //{
        //    base.OnStartup(e);

        //    IUnityContainer container = new UnityContainer();
        //    // register viewModel
        //    container.RegisterType<ViewModelBase, WifiViewModel>();

        //    // register service

        //    // show main dialog window 
        //    MainWindow mainWindow = container.Resolve<MainWindow>();
        //    mainWindow.Show(); // msut be remove 'StartupUri="MainWindow.xaml"' attribute in App.xaml
        //}
    }
}
