using RTKModule;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using UserTool.Utility;

namespace UserTool.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public string Version { get { return "RTK User Tool v1.00d"; } }
    
        private ViewModelBase currentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get { return currentViewModel; }
            set { currentViewModel = value; }
        }

        public MainViewModel(ViewModelBase viewModel)
        {
            currentViewModel = viewModel;
        }

        public MainViewModel() : this(new WifiViewModel())
        {
        }
    }
}
