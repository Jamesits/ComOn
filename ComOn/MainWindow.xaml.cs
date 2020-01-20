using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
        public char SelectedFlowControlMode { get; set; } = 'X';
        public string PuttyFilePath { get; set; } = Utils.GetFullPath("putty.exe");
        public string PuttyAdditionalArguments { get; set; } = "";

        public bool CanLaunch => !(string.IsNullOrWhiteSpace(SelectedComPortName) || string.IsNullOrWhiteSpace(PuttyFilePath));
        public static string VersionString => Utils.GetVersion().ToString();
        public GitHubReleaseChecker ReleaseChecker { get; } = new GitHubReleaseChecker("Jamesits", "ComOn");
        public bool HaveUpdate { get; private set; }
        public Uri UpdateDownloadUri { get; private set; }

        private readonly string[] _updateStatusStrings = new[]
        {
            "waiting",
            "checking updates",
            "update available",
            "latest version",
            "update check failed",
        };

        public string ReleaseCheckerStatus { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ComPortViewModel;
            ReleaseChecker.UpdateCheckStatusChanged += ReleaseChecker_StateChanged;
        }

        private void ReleaseChecker_StateChanged(object sender, EventArgs e)
        {
            ReleaseCheckerStatus = _updateStatusStrings[(int) ReleaseChecker.UpdateCheckStatus];
            HaveUpdate = (ReleaseChecker.UpdateCheckStatus == GitHubReleaseChecker.CheckStatus.HaveUpdate);
            UpdateDownloadUri = ReleaseChecker.UpdatePageLinkUri;
        }

        private void BtnSelectPuttyExecutable_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                CheckFileExists = true,
                CheckPathExists = true,
                DereferenceLinks = true,
                FileName = "putty.exe",
                InitialDirectory = @"C:\Program Files\Putty",
                RestoreDirectory = true,
                Filter = "PE Executables (*.exe)|*.exe|All files (*.*)|*.*",
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

#if (NETCOREAPP)
                    // .net core specific
                    newPuttyProcess.StartInfo.ArgumentList.Add("-serial");
                    newPuttyProcess.StartInfo.ArgumentList.Add("-sercfg");
                    newPuttyProcess.StartInfo.ArgumentList.Add($"{SelectedBaudRate},{SelectedDataBits},{SelectedParity},{SelectedStopBit},{SelectedFlowControlMode}");
                    newPuttyProcess.StartInfo.ArgumentList.Add($"{port}");
#endif

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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // set default values for the controls
            if (ComPortViewModel.ComPorts.Count > 0)
            {
                SelectedComPort = ComPortViewModel.ComPorts.LastOrDefault();
            }

            await ReleaseChecker.RefreshReleaseInfo().ConfigureAwait(false);
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

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void LblGitHubLink_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/Jamesits/ComOn");
            e.Handled = true;
        }

        private async void LblDownloadUpdateLink_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (HaveUpdate && UpdateDownloadUri != null)
            {
                Process.Start(UpdateDownloadUri.ToString());
            }
            else
            {
                await ReleaseChecker.RefreshReleaseInfo().ConfigureAwait(false);
            }
        }
    }
}
