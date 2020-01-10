using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.IO.Ports;
using System.Linq;

namespace ComOn
{
    public class ComPort
    {
        public string Id { get; set; }
        public ulong Order { get; set; }
        public string DisplayName { get; set; }
    }
    public class ComPortViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ComPortViewModel()
        {
            RefreshComPortList();
        }

        public ObservableCollection<ComPort> ComPorts { get; set; }

        public ObservableCollection<ulong> SerialBaudRates => new ObservableCollection<ulong>
        {
            300,
            1200,
            2400,
            4800,
            9600,
            19200,
            38400,
            57600,
            74880,
            115200,
            230400,
            250000,
            500000,
            921600,
            1000000,
            2000000,
        };

        public Dictionary<char, string> SerialParityModes => new Dictionary<char, string>
        {
            { 'n', "None" },
            { 'o', "Odd" },
            { 'e', "Even" },
            { 'm', "Mark" },
            { 's', "Space" },
        };

        public ObservableCollection<ulong> SerialStopBits => new ObservableCollection<ulong>
        {
            1,
            2,
        };

        public ObservableCollection<ulong> SerialDataBits => new ObservableCollection<ulong>
        {
            5,
            6,
            7,
            8,
            9,
        };

        public Dictionary<char, string> SerialFlowControlModes => new Dictionary<char, string>
        {
            { 'N', "None" },
            { 'X', "XON/XOFF" },
            { 'R', "RTS/CTS" },
            { 'D', "DSR/DTR" },
        };

        public void RefreshComPortList()
        {
            var newComPorts = new List<ComPort>();

            foreach (var port in SerialPort.GetPortNames())
            {
                newComPorts.Add(new ComPort
                {
                    Id = port,
                    DisplayName = port,
                    Order = ulong.Parse(new string(port.Where(char.IsNumber).ToArray())),
                });
            }
            
            // try if we can get a description -- might fail when running during WM_DEVICECHANGE
            var mbs = new ManagementObjectSearcher("Select * From Win32_SerialPort");
            try
            {
                var mbsList = mbs.Get();

                foreach (var o in mbsList)
                {
                    var mo = (ManagementObject)o;
                    newComPorts.Where(x => x.Id == mo["DeviceID"].ToString()).All(x =>
                    {
                        x.DisplayName = mo["Name"].ToString();
                        return true;
                    });
                }
            }
            catch (System.InvalidCastException ex)
            {
                Debug.WriteLine(ex.Message);
            }

            ComPorts = new ObservableCollection<ComPort>(newComPorts.OrderBy(x => x.Order));
        }
    } 
}

