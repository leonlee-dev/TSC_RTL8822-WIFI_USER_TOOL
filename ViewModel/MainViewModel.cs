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
        private readonly string adbPath = Environment.CurrentDirectory + "\\platform-tools\\adb.exe";

        // connector
        private Adb adb;
        private ComPort comPort;

        private ILog log = new Log();
        private RtwProxyProcessor rtwProxyProcessor;
        private DispatcherTimer updateRxCountTimer;
        private DispatcherTimer saveLogTimer;
        private DispatcherTimer updateComPortTimer;
        private Regex numericRegex = new Regex(@"^[0-9]+$");
        private Regex base16Regex = new Regex(@"^[0-9A-Fa-f]+$");
        private bool isTxSend = false;
        private bool isRxSend = false;

        public IEnumerable<int> FrequencyItemsSource { get { return Array.ConvertAll<CH, int>(Wifi.chDic.Keys.ToArray(), delegate (CH ch) { return (int)ch; }); } }
        public IEnumerable<string> BandwidthItemsSource { get { return Wifi.bwDic.Values; } }
        public IEnumerable<string> RateIDItemsSource { get { return Wifi.rateIdDic.Values; } }
        public IEnumerable<string> AntennaItemsSource { get { return Wifi.antPathDic.Values.TakeWhile((value) => !value.Equals(Wifi.antPathDic[ANT_PATH.PATH_AB])); } }
        public IEnumerable<string> ComPortItemsSource;

        private string strConnect = "Conn";
        public string StrConnect { get { return strConnect; } set { strConnect = value; OnPropertyChanged("StrConnect"); } }

        private string strTxSend = "Tx Send";
        public string StrTxSend { get { return strTxSend; } set { strTxSend = value; OnPropertyChanged("strTxSend"); } }

        private string strRxSend = "Rx Send";
        public string StrRxSend { get { return strRxSend; } set { strRxSend = value; OnPropertyChanged("StrRxSend"); } }

        private bool isInit = false;
        public bool IsInit { get { return isInit; } set { isInit = value; OnPropertyChanged("IsInit"); } }

        private bool isConnected = false;
        public bool IsConnected { get { return isConnected; } set { isConnected = value; OnPropertyChanged("IsConnected"); } }

        private bool isUsbUsed = true;
        public bool IsUsbUsed { get { return isUsbUsed; } set { isUsbUsed = value; OnPropertyChanged("IsUsbUsed"); } }

        private bool isComPortUsed = false;
        public bool IsComPortUsed { get { return isComPortUsed; } set { isComPortUsed = value; OnPropertyChanged("IsComPortUsed"); } }

        private bool isBTClosed = false;
        public bool IsBTClosed
        {
            get { return isBTClosed; }
            set
            {
                if (isInit)
                {
                    isBTClosed = value;
                    RtwCommand.EnableBT(isBTClosed);
                    OnPropertyChanged("IsBTClosed");
                }
            }
        }

        private bool isDefaultTxPower = true;
        public bool IsDefaultTxPower { get { return isDefaultTxPower; } set { isDefaultTxPower = value; OnPropertyChanged("IsDefaultTxPower"); } }

        private bool autoRx = true;
        public bool AutoRx { get { return autoRx; } set { if (!isRxSend) autoRx = value; OnPropertyChanged("AutoRx"); } }

        private int crystal;
        public string CrystalBase16
        {
            get { return crystal != 0 ? crystal.ToString("x2") : ""; }
            set
            {
                string strCrystal = value;
                if (string.IsNullOrEmpty(value))
                    strCrystal = "0";

                if (base16Regex.IsMatch(strCrystal))
                {
                    int v = Convert.ToInt32(strCrystal, 16);
                    if (v >= 0 && v <= 0x7f)
                    {
                        crystal = v;
                        OnPropertyChanged("CrystalBase16");
                    }
                }
            }
        }

        private int frequency;
        public int Frequency { get { return frequency; } set { frequency = value; OnPropertyChanged("Frequency"); } }

        private string bandWidth;
        public string BandWidth { get { return bandWidth; } set { bandWidth = value; OnPropertyChanged("BandWidth"); } }

        private string rateId;
        public string RateID { get { return rateId; } set { rateId = value; OnPropertyChanged("RateID"); } }

        private string antenna;
        public string Antenna { get { return antenna; } set { antenna = value; OnPropertyChanged("Antenna"); } }

        private string comNum;
        public string ComNum { get { return comNum; } set { comNum = value; OnPropertyChanged("ComNum"); } }

        private string txPowerBase16;
        public string TxPowerBase16
        {
            get
            {
                return txPowerBase16;
            }
            set
            {
                txPowerBase16 = value;
                OnPropertyChanged("TxPowerBase16");
            }
        }

        private int txPower;
        public string TxPower
        {
            get
            {
                return txPower != 0 ? txPower.ToString() : "";
            }
            set
            {
                string strTxPower = value;
                if (string.IsNullOrEmpty(value))
                    strTxPower = "0";

                if (numericRegex.IsMatch(strTxPower))
                {
                    int v = Convert.ToInt32(strTxPower);
                    if (v >= 0 && v <= 0x7f)
                    {
                        txPower = v;
                        TxPowerBase16 = "0x" + v.ToString("x2");
                        OnPropertyChanged("TxPower");
                    }
                }
            }
        }

        private int rxCount = 0;
        public int RxCount { get { return rxCount; } set { rxCount = value; OnPropertyChanged("RxCount"); } }

        private string rtbtext;
        public string RtbText { get { return rtbtext; } set { rtbtext = value; OnPropertyChanged("RtbText"); } }

        public ICommand InitCommand
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                    {
                        RtwInterfaceSetup();
                        RtwCommand.Init();
                        IsInit = true;
                        IsBTClosed = true;
                    }
                },
                () => true);
            }
        }

        public ICommand ConnectionCommand
        {
            get
            {
                return new CommandBase((o) =>
          {
              if (!isConnected)
              {
                  if (!ConnectDUT())
                      return;

                  StrConnect = "Disconn";
                  IsConnected = true;
              }
              else
              {
                  DisconnectDUT();
                  StrConnect = "Conn";
                  IsConnected = false;
                  IsInit = false;

                  isTxSend = false;
                  isRxSend = false;
                  StrTxSend = "Tx Send";
                  StrRxSend = "Rx Send";
              }
          }
          , () => true);
            }
        }

        public ICommand CrystalInputEnter
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_phypara xcap=" + crystal);
                },
                () => true);
            }
        }

        public ICommand TxPowerInputEnter
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    ANT_PATH ANT = Wifi.antPathDic.FirstOrDefault(x => x.Value == antenna).Key;
                    if (ANT == ANT_PATH.PATH_A)
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower + ",pathb=0");
                    else if (ANT == ANT_PATH.PATH_B)
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=0,pathb=" + txPower);
                },
                () => true);
            }
        }

        public ICommand TxPowerIncreased
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    if (isRxSend)
                        return;

                    TxPower = (txPower + 1).ToString();
                    ANT_PATH ANT = Wifi.antPathDic.FirstOrDefault(x => x.Value == antenna).Key;
                    if (ANT == ANT_PATH.PATH_A)
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower + ",pathb=0");
                    else if (ANT == ANT_PATH.PATH_B)
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=0,pathb=" + txPower);
                    //RtwCommand.SendTxPowerCommand(ANT, (byte)txPower);
                },
                () => true);
            }
        }

        public ICommand TxPowerDecreased
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    if (isRxSend)
                        return;

                    TxPower = (txPower - 1).ToString();
                    ANT_PATH ANT = Wifi.antPathDic.FirstOrDefault(x => x.Value == antenna).Key;
                    if (ANT == ANT_PATH.PATH_A)
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower + ",pathb=0");
                    else if (ANT == ANT_PATH.PATH_B)
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=0,pathb=" + txPower);
                },
                () => true);
            }
        }

        public ICommand ResetRxCount
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    if (!isRxSend)
                        return;

                    RxCount = 0;
                    RtwCommand.ResetRxStat();
                },
                () => true);
            }
        }

        public ICommand GetRxCount
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    if (!isRxSend)
                        return;

                    RxCount = RtwCommand.GetRxPacketCount();
                },
                () => true);
            }
        }

        public ICommand TxSendCommand
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    if (isRxSend)
                        return;

                    if (!isTxSend)
                    {
                        BW BW = Wifi.bwDic.FirstOrDefault(x => x.Value == bandWidth).Key;
                        ANT_PATH ANT = Wifi.antPathDic.FirstOrDefault(x => x.Value == antenna).Key;

                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_start");
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_channel " + Wifi.ChannelMapping(frequency));
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_bandwidth 40M=" + (int)BW + ",shortGI=0");
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_rate " + rateId);
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_ant_tx " + antenna);
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_phypara xcap=" + crystal);
                        if (isDefaultTxPower)
                        {
                            int[] txPower = RtwCommand.GetTxPower(ANT);
                            if (ANT == ANT_PATH.PATH_A)
                                rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower[0] + ",pathb=0");
                            else if (ANT == ANT_PATH.PATH_B)
                                rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=0,pathb=" + txPower[0]);
                            TxPower = txPower[0].ToString();
                        }
                        else
                        {
                            if (ANT == ANT_PATH.PATH_A)
                                rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower + ",pathb=0");
                            else if (ANT == ANT_PATH.PATH_B)
                                rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=0,pathb=" + txPower);
                        }
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx background,pkt");
                        StrTxSend = "Stop Tx";
                        isTxSend = true;
                    }
                    else
                    {
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx stop");
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_stop");
                        StrTxSend = "Tx Send";
                        isTxSend = false;
                    }
                },
                () => true);
            }
        }

        public ICommand RxSendCommand
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                        return;

                    if (isTxSend)
                        return;

                    if (!isRxSend)
                    {
                        BW BW = Wifi.bwDic.FirstOrDefault(x => x.Value == bandWidth).Key;

                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_start");
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_channel " + Wifi.ChannelMapping(frequency));
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_ant_rx " + antenna);
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_bandwidth 40M=" + (int)BW + ",shortGI=0");
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_arx start");
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_reset_stats");
                        StrRxSend = "Stop Rx";
                        isRxSend = true;

                        if (autoRx)
                            updateRxCountTimer.Start();
                    }
                    else
                    {
                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_stop");
                        StrRxSend = "Rx Send";
                        isRxSend = false;

                        if (updateRxCountTimer.IsEnabled)
                            updateRxCountTimer.Stop();
                    }
                },
                () => true);
            }
        }

        public ICommand Loaded
        {
            get
            {
                return new CommandBase((o) =>
                {
                    // default value
                    Frequency = (int)CH.CH14;
                    BandWidth = Wifi.bwDic[BW.B_20MHZ];
                    RateID = Wifi.rateIdDic[RATE_ID.R_54M];
                    Antenna = Wifi.antPathDic[ANT_PATH.PATH_B];
                    CrystalBase16 = "50"; // base 16
                    TxPower = "80"; // base 10
                },
                () => true);
            }
        }

        public ICommand ClosedEvent
        {
            get
            {
                return new CommandBase((o) =>
                {
                    updateRxCountTimer.Stop();
                    saveLogTimer.Stop();
                    updateComPortTimer.Stop();
                    //System.Windows.Application.Current.MainWindow.Close();
                },
                () => true);
            }
        }

        private bool ConnectDUT()
        {
            if (isUsbUsed)
            {
                adb = new Adb(adbPath);
                adb.ReceiveAdbMessageEvent += new Adb.ReceiveAdbMessageEventHandler(ProcessReceive);
                adb.ExitAdbEvent += new Adb.ExitAdbEventHandler(ProcessExit);
                if (!adb.OpenAdbShell())
                {
                    adb.Close();
                    adb = null;
                    return false;
                }
            }
            else if (isComPortUsed)
            {
                if (string.IsNullOrEmpty(comNum))
                    return false;

                comPort = new ComPort(comNum, 460800, 8, StopBits.One, Parity.None);
                comPort.ReceiveSerialMessageEvent += ProcessSerialReceive;
                comPort.Open();
            }
            return true;
        }

        private void DisconnectDUT()
        {
            if (isUsbUsed)
            {
                if (adb != null)
                {
                    adb.Close();
                    adb = null;
                }
            }
            else if (isComPortUsed)
            {
                if (comPort != null)
                {
                    comPort.Close();
                    comPort = null;
                }
            }
        }

        public void RtwInterfaceSetup()
        {
            RtwLogHandledInterceptor rtwLogHandledInterceptor = new RtwLogHandledInterceptor(log);
            if (isUsbUsed)
                rtwProxyProcessor = RtwProxyCreator.CreatProxy(adb, rtwLogHandledInterceptor);
            else if (IsComPortUsed)
                rtwProxyProcessor = RtwProxyCreator.CreatProxy(comPort, rtwLogHandledInterceptor);
            RtwCommand.SetRtwProxyProcessor(rtwProxyProcessor);
        }

        //private string currentTime;
        //public string CurrentTime
        //{
        //    get { return currentTime; }
        //    set { currentTime = value; OnPropertyChanged("CurrentTime"); }
        //}

        public MainViewModel()
        {
            updateRxCountTimer = new DispatcherTimer();
            updateRxCountTimer.Interval = TimeSpan.FromMilliseconds(100);
            updateRxCountTimer.Tick += (sender, args) =>
            {
                GetRxCount.Execute(null);
            };

            saveLogTimer = new DispatcherTimer();
            saveLogTimer.Interval = TimeSpan.FromMilliseconds(1000);
            saveLogTimer.Tick += (sender, args) =>
            {
                if (log != null && log.Read().Length > 0)
                {
                    string dir = Environment.CurrentDirectory + "\\Adb-Log";
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    string file = "adb_" + DateTime.Now.ToString("yyyyMMdd");
                    ((Log)log).SaveWithAppend(dir + "\\" + file + ".txt");
                    log.Clear();
                }
            };
            saveLogTimer.Start();

            updateComPortTimer = new DispatcherTimer();
            updateComPortTimer.Interval = TimeSpan.FromMilliseconds(2000);
            updateComPortTimer.Tick += (sender, args) =>
            {
                if (!isComPortUsed)
                    return;

                string[] comPorts = SerialPort.GetPortNames();
                int count = comPorts.Count();
                if (count > 0)
                    ComPortItemsSource = comPorts;
            };
            updateComPortTimer.Start();
        }

        private void ProcessReceive(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            if (rtwProxyProcessor != null)
            {
                Console.WriteLine(e.Data);
                rtwProxyProcessor.Receive(e.Data);
                RtbText += e.Data + "\r\n";
            }
        }

        private void ProcessSerialReceive(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = sender as SerialPort;
            string data = sp.ReadExisting();
            if (string.IsNullOrEmpty(data))
                return;

            if (rtwProxyProcessor != null)
            {
                rtwProxyProcessor.Receive(data);
                RtbText += data;
            }
        }

        private void ProcessExit(object sender, EventArgs e)
        {

        }
    }
}
