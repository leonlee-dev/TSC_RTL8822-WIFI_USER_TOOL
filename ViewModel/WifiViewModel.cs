using RTKModule;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using UserTool.Utility;

namespace UserTool.ViewModel
{
    public class WifiViewModel : ViewModelBase
    {
        private readonly string adbPath = Environment.CurrentDirectory + "\\platform-tools\\adb.exe";
        private readonly string configPath = Environment.CurrentDirectory + "\\config.txt";

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

        //RtwCommand.Init("8822cs.ko", "/vendor/lib/modules/");
        //RtwCommand.Init("88x2cs.ko", "/lib/modules/4.9.241-BPI-M5/kernel/drivers/net/wireless/realtek/rtl8822cs/");
        private bool enablePowerLimitTable = false;
        private bool enablePowerByRateTable = false;
        private string drivFile = "8822cs.ko";
        private string drivDir = "/vendor/lib/modules/";
        private const int BAUDRATE = 115200;

        private RtwCommand rtwCommand;

        public IEnumerable<int> FrequencyItemsSource { get { return Array.ConvertAll<CH, int>(Wifi.chDic.Keys.ToArray(), delegate (CH ch) { return (int)ch; }); } }
        public IEnumerable<string> BandwidthItemsSource { get { return Wifi.bwDic.Values; } }
        public IEnumerable<string> RateIDItemsSource { get { return Wifi.rateIdDic.Values; } }
        public IEnumerable<string> TxModeItemsSource { get { return Wifi.txModeDic.Values; } }
        public ObservableCollection<string> AntennaItemsSource { get; set; } = new ObservableCollection<string>(); // bound by address, so can't new one

        public IEnumerable<string> comPortItemsSource;
        public IEnumerable<string> ComPortItemsSource { get { return comPortItemsSource; } set { comPortItemsSource = value; OnPropertyChanged("ComPortItemsSource"); } }

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
                //if (isInit)
                //{
                //    isBTClosed = value;
                //    Task.Run(() =>
                //    {
                //        rtwCommand.EnableBT(isBTClosed);
                //    });

                //    OnPropertyChanged("IsBTClosed");
                //}
            }
        }

        private bool isEnablePowerTracking = false;
        public bool IsEnablePowerTracking
        {
            get { return isEnablePowerTracking; }
            set
            {
                if (isInit)
                {
                    isEnablePowerTracking = value;
                    Task.Run(() =>
                    {
                        if (isEnablePowerTracking)
                            rtwCommand.WifiPowerTracking(1);
                        else
                            rtwCommand.WifiPowerTracking(0);
                    });
                    OnPropertyChanged("IsEnablePowerTracking");
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
                if (string.IsNullOrEmpty(value))
                    value = "0";

                if (base16Regex.IsMatch(value))
                {
                    int v = Convert.ToInt32(value, 16);
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
        public string RateID
        {
            get { return rateId; }
            set
            {
                if (rateId == value)
                    return;

                rateId = value;
                RATE_ID rate = Wifi.rateIdDic.FirstOrDefault(x => x.Value == rateId).Key;
                if (rate >= RATE_ID.VHT2MCS0 && rate <= RATE_ID.VHT2MCS9)
                {
                    //System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(AntennaItemsSource, System.Runtime.InteropServices.GCHandleType.WeakTrackResurrection);
                    //int address = System.Runtime.InteropServices.GCHandle.ToIntPtr(handle).ToInt32();
                    //Console.WriteLine("A" + address);
                    //handle = System.Runtime.InteropServices.GCHandle.Alloc(antennaItemsSource, System.Runtime.InteropServices.GCHandleType.WeakTrackResurrection);
                    //address = System.Runtime.InteropServices.GCHandle.ToIntPtr(handle).ToInt32();
                    //Console.WriteLine("a" + address);
                    AntennaItemsSource.Clear();
                    AntennaItemsSource.Add(Wifi.antPathDic[ANT_PATH.PATH_AB]);
                    Antenna = Wifi.antPathDic[ANT_PATH.PATH_AB];
                }
                else
                {
                    AntennaItemsSource.Clear();
                    AntennaItemsSource.Add(Wifi.antPathDic[ANT_PATH.PATH_A]);
                    AntennaItemsSource.Add(Wifi.antPathDic[ANT_PATH.PATH_B]);
                    Antenna = Wifi.antPathDic[ANT_PATH.PATH_A];
                }
                OnPropertyChanged("RateID");
            }
        }

        private string antenna;
        public string Antenna { get { return antenna; } set { antenna = value; OnPropertyChanged("Antenna"); } }

        private string txMode;
        public string TxMode { get { return txMode; } set { txMode = value; OnPropertyChanged("TxMode"); } }

        private string comNum;
        public string ComNum { get { return comNum; } set { comNum = value; OnPropertyChanged("ComNum"); } }

        private int txCount = 0;
        public string TxCount
        {
            get
            {
                return txCount != 0 ? txCount.ToString() : "";
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = "0";

                if (numericRegex.IsMatch(value))
                {
                    txCount = Convert.ToInt32(value);
                    OnPropertyChanged("TxCount");
                }
            }
        }

        private string txPower0Base16;
        public string TxPower0Base16
        {
            get
            {
                return txPower0Base16;
            }
            set
            {
                txPower0Base16 = value;
                OnPropertyChanged("TxPower0Base16");
            }
        }

        private string txPower1Base16;
        public string TxPower1Base16
        {
            get
            {
                return txPower1Base16;
            }
            set
            {
                txPower1Base16 = value;
                OnPropertyChanged("TxPower1Base16");
            }
        }

        private int txPower0;
        public string TxPower0
        {
            get
            {
                return txPower0 != 0 ? txPower0.ToString() : "";
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = "0";

                if (numericRegex.IsMatch(value))
                {
                    int v = Convert.ToInt32(value);
                    if (v >= 0 && v <= 0x7f)
                    {
                        txPower0 = v;
                        TxPower0Base16 = "0x" + v.ToString("x2");
                        OnPropertyChanged("TxPower0");
                    }
                }
            }
        }

        private int txPower1;
        public string TxPower1
        {
            get
            {
                return txPower1 != 0 ? txPower1.ToString() : "";
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    value = "0";

                if (numericRegex.IsMatch(value))
                {
                    int v = Convert.ToInt32(value);
                    if (v >= 0 && v <= 0x7f)
                    {
                        txPower1 = v;
                        TxPower1Base16 = "0x" + v.ToString("x2");
                        OnPropertyChanged("TxPower1");
                    }
                }
            }
        }

        private int rxCount = 0;
        public int RxCount { get { return rxCount; } set { rxCount = value; OnPropertyChanged("RxCount"); } }

        private string rtbtext;
        public string RtbText { get { return rtbtext; } set { rtbtext = value; OnPropertyChanged("RtbText"); } }

        public ICommand ReadWLCommand
        {
            get
            {
                return new CommandBase((o) =>
                {
                    Task.Run(() =>
                    {
                        if (isInit)
                        {
                            rtwCommand.ReadFromWLEfuse();
                        }
                    });
                },
                () => true);
            }
        }

        public ICommand ReadBTCommand
        {
            get
            {
                return new CommandBase((o) =>
                {
                    Task.Run(() =>
                    {
                        if (isInit)
                        {
                            rtwCommand.ReadFromBTEfuse();
                        }
                    });
                },
                () => true);
            }
        }

        public ICommand InitCommand
        {
            get
            {
                return new CommandBase((o) =>
                {
                    if (!isInit)
                    {
                        RtwInterfaceSetup();
                        Task.Run(() =>
                        {
                            rtwCommand.Init(drivFile, drivDir, enablePowerLimitTable, enablePowerByRateTable);
                        });
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

                    //ANT_PATH ANT = Wifi.antPathDic.FirstOrDefault(x => x.Value == antenna).Key;
                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower0 + ",pathb=" + txPower1);
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

                    int tx = int.Parse(o as string);
                    if (tx == 0)
                        TxPower0 = (txPower0 + 1).ToString();
                    else if (tx == 1)
                        TxPower1 = (txPower1 + 1).ToString();
                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower0 + ",pathb=" + txPower1);
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

                    int tx = int.Parse(o as string);
                    if (tx == 0)
                        TxPower0 = (txPower0 - 1).ToString();
                    else if (tx == 1)
                        TxPower1 = (txPower1 - 1).ToString();
                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower0 + ",pathb=" + txPower1);
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
                    rtwCommand.ResetRxStat();
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

                    RxCount = rtwCommand.GetRxPacketCount();
                },
                () => true);
            }
        }

        public ICommand TxSendCommand
        {
            get
            {
                return new CommandBase(async (o) =>
                {
                    if (!isInit)
                        return;

                    if (isRxSend)
                        return;

                    if (!isTxSend)
                    {
                        BW BW = Wifi.bwDic.FirstOrDefault(x => x.Value == bandWidth).Key;
                        ANT_PATH ANT = Wifi.antPathDic.FirstOrDefault(x => x.Value == antenna).Key;
                        TX_MODE TXMODE = Wifi.txModeDic.FirstOrDefault(x => x.Value == txMode).Key;

                        await Task.Run(() =>
                        {
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_start", 1000);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_channel " + Wifi.ChannelMapping(frequency), 1000);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_bandwidth 40M=" + (int)BW + ",shortGI=0", 1000);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_rate " + rateId, 1000);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_ant_tx " + antenna, 1000);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_phypara xcap=" + crystal, 1000);
                            if (isDefaultTxPower)
                            {
                                int[] txPower = rtwCommand.GetTxPower(ANT);
                                if (ANT == ANT_PATH.PATH_A)
                                {
                                    TxPower0 = txPower[0].ToString();
                                }
                                else if (ANT == ANT_PATH.PATH_B)
                                {
                                    TxPower1 = txPower[0].ToString();
                                }
                                else if (ANT == ANT_PATH.PATH_AB)
                                {
                                    TxPower0 = txPower[0].ToString();
                                    TxPower1 = txPower[1].ToString();
                                }
                                rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower0 + ",pathb=" + txPower1, 1000);
                            }
                            else
                            {
                                rtwProxyProcessor.Send("rtwpriv wlan0 mp_txpower patha=" + txPower0 + ",pathb=" + txPower1, 1000);
                            }

                            if (isEnablePowerTracking)
                                rtwCommand.WifiPowerTracking(1);

                            switch (TXMODE)
                            {
                                case TX_MODE.PACKET_TX:
                                    if (txCount != 0)
                                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx count=" + txCount + ",pkt", 1000);
                                    else
                                        rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx background,pkt", 1000);
                                    break;
                                case TX_MODE.CONTINUOUS_TX:
                                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx background");
                                    break;
                                case TX_MODE.CARRIER_SUPPRESION:
                                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx background,cs");
                                    break;
                                case TX_MODE.SINGLE_TONE_TX:
                                    rtwProxyProcessor.Send("rtwpriv wlan0 mp_ctx background,stone");
                                    break;
                            }
                        });

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
                return new CommandBase(async (o) =>
                {
                    if (!isInit)
                        return;

                    if (isTxSend)
                        return;

                    if (!isRxSend)
                    {
                        BW BW = Wifi.bwDic.FirstOrDefault(x => x.Value == bandWidth).Key;

                        await Task.Run(() =>
                        {
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_start", 100);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_channel " + Wifi.ChannelMapping(frequency), 100);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_ant_rx " + antenna, 100);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_bandwidth 40M=" + (int)BW + ",shortGI=0", 100);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_arx start", 100);
                            rtwProxyProcessor.Send("rtwpriv wlan0 mp_reset_stats", 100);
                        });

                        RxCount = 0;
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
                    AntennaItemsSource.Add(Wifi.antPathDic[ANT_PATH.PATH_A]);
                    AntennaItemsSource.Add(Wifi.antPathDic[ANT_PATH.PATH_B]);

                    // default value
                    Frequency = (int)CH.CH14;
                    BandWidth = Wifi.bwDic[BW.B_20MHZ];
                    RateID = Wifi.rateIdDic[RATE_ID.R_54M];
                    Antenna = Wifi.antPathDic[ANT_PATH.PATH_B];
                    TxMode = Wifi.txModeDic[TX_MODE.PACKET_TX];
                    CrystalBase16 = "50"; // base 16
                    TxPower0 = "80"; // base 10
                    TxPower1 = "80"; // base 10

                    try
                    {
                        // load config
                        string[] lines = File.ReadAllLines(configPath);
                        enablePowerLimitTable = lines.Select(line => line.StartsWith("POWER_LIMIT_TABLE") ? line : "").SkipWhile(content => string.IsNullOrEmpty(content)).First().Split('=')[1].Trim() == "0" ? false : true;
                        enablePowerByRateTable = lines.Select(line => line.StartsWith("POWER_BY_RATE_TABLE") ? line : "").SkipWhile(content => string.IsNullOrEmpty(content)).First().Split('=')[1].Trim() == "0" ? false : true;
                        drivFile = lines.Select(line => line.StartsWith("KO_FILE") ? line : "").SkipWhile(content => string.IsNullOrEmpty(content)).First().Split('=')[1];
                        drivDir = lines.Select(line => line.StartsWith("KO_DIR") ? line : "").SkipWhile(content => string.IsNullOrEmpty(content)).First().Split('=')[1];
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                },
                () => true);
            }
        }

        public ICommand Unloaded
        {
            get
            {
                return new CommandBase((o) =>
                {
                    updateRxCountTimer.Stop();
                    saveLogTimer.Stop();
                    updateComPortTimer.Stop();
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

                comPort = new ComPort(comNum, BAUDRATE, 8, StopBits.One, Parity.None);
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
            rtwCommand = new RtwCommand(rtwProxyProcessor);
        }

        public WifiViewModel()
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
