using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ComOn
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ComPortViewModel ComPortViewModel { get; } = new ComPortViewModel();
        public ComPort SelectedComPort { get; set; }
        public string SelectedComPortName { get; set; }
        public ulong SelectedBaudRate { get; set; } = 9600;
        public char SelectedParity { get; set; } = 'n';
        public ulong SelectedStopBit { get; set; } = 1;
        public ulong SelectedDataBits { get; set; } = 8;
        public char SelectedFlowControlMode { get; set; } = 'N';
        public string PuttyFilePath { get; set; } = Utils.GetFullPath("putty.exe");
        public string PuttyAdditionalArguments { get; set; } = "";
        public MainWindow()
        {
            InitializeComponent();
            DataContext = ComPortViewModel;
        }

        private void BtnSelectPuttyExecutable_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                DefaultExt = ".exe",
                CheckFileExists = true,
                CheckPathExists = true,
                DereferenceLinks = true,
                FileName = "putty.exe",
                InitialDirectory = @"C:\Program Files\Putty",
            };
            if (openFileDialog.ShowDialog() != true) return;
            PuttyFilePath = openFileDialog.FileName;
        }

        private void BtnRunPutty_Click(object sender, RoutedEventArgs e)
        {
            var port = SelectedComPortName;
            if (SelectedComPort != null) port = SelectedComPort.Id;

            try
            {
                using (var newPuttyProcess = new Process())
                {
                    newPuttyProcess.StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        FileName = PuttyFilePath,
                        Arguments = $"-serial -sercfg {SelectedBaudRate},{SelectedDataBits},{SelectedParity},{SelectedStopBit},{SelectedFlowControlMode} {PuttyAdditionalArguments} {port}",
                    };

                    // .net core only
                    //newPuttyProcess.StartInfo.ArgumentList.Add("-serial");
                    //newPuttyProcess.StartInfo.ArgumentList.Add("-sercfg");
                    //newPuttyProcess.StartInfo.ArgumentList.Add($"{SelectedBaudRate},{SelectedDataBits},{SelectedParity},{SelectedStopBit},{SelectedFlowControlMode}");
                    //newPuttyProcess.StartInfo.ArgumentList.Add($"{port}");

                    //Debug.Write("Launch arguments: ");
                    //foreach (var i in newPuttyProcess.StartInfo.ArgumentList)
                    //{
                    //    Debug.Write(i + " ");
                    //}
                    //Debug.WriteLine("");

                    newPuttyProcess.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void BtnRefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            ComPortViewModel.RefreshComPortList();
            if (ComPortViewModel.ComPorts.Count > 0)
            {
                SelectedComPort = ComPortViewModel.ComPorts.LastOrDefault();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // set default values for the controls
            if (ComPortViewModel.ComPorts.Count > 0)
            {
                SelectedComPort = ComPortViewModel.ComPorts.LastOrDefault();
            }
        }

        #region Hardware change event handling
        // https://stackoverflow.com/a/55064587

        private UsbEventRegistration _usbEventRegistration;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // IMO this should be abstracted away from the code-behind.
            var windowSource = (HwndSource)PresentationSource.FromVisual(this);
            _usbEventRegistration = new UsbEventRegistration(windowSource.Handle);
            // This will allow your window to receive USB events.
            _usbEventRegistration.Register();
            // This hook is what we were aiming for. All Windows events are listened to here. We can inject our own listeners.
            windowSource.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Here's where the help ends. Do what you need here.
            // Get additional info from http://www.pinvoke.net/
            // USB event message is msg == 0x0219 (WM_DEVICECHANGE).
            // USB event plugin is wParam == 0x8000 (DBT_DEVICEARRIVAL).
            // USB event unplug is wParam == 0x8004 (DBT_DEVICEREMOVECOMPLETE).
            // Your device info is in lParam. Filter that.
            // You need to convert wParam/lParam to Int32 using Marshal.
            if (msg == 0x0219 && (wParam.ToInt64() == 0x8000) || (wParam.ToInt64() == 0x8004))
            {
                Debug.WriteLine("Device list changed, refreshing COM port list");
                ComPortViewModel.RefreshComPortList();
            }
            
            return IntPtr.Zero;
        }
        #endregion

    }
}
