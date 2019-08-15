using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;

using SharpBroadlink;
using SharpBroadlink.Devices;

namespace BroadlinkControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum CommandType { IR, RF }

        #region constructors

        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region properties

        private IDevice[] Devices { get; set; }

        private byte[] LatestCommand { get; set; }

        private CommandType LatestCommandType { get; set; }

        #endregion

        #region event handlers

        private async void _buttonScanNetwork_Click(object sender, RoutedEventArgs e)
        {
            await SearchForDevices();
        }

        private async void _buttonLearnIr_Click(object sender, RoutedEventArgs e)
        {
            if (Devices.Length > 0)
            {
                try
                {
                    await LearnIr();
                }
                catch (Exception)
                {
                    AddOutputText("Command learning cancelled");
                }
            }
        }

        private async void _buttonLearnRf_Click(object sender, RoutedEventArgs e)
        {
            if (Devices.Length > 0)
            {
                try
                {
                    await LearnRf();
                }
                catch(Exception)
                {
                    AddOutputText("Command learning cancelled");
                }
            }
        }

        private async void _buttonSend_Click(object sender, RoutedEventArgs e)
        {
            if (Devices.Length > 0 &&
                LatestCommand != null &&
                LatestCommand.Length > 0)
            {
                try
                {
                    await Send();
                }
                catch (Exception)
                {
                    AddOutputText("Command sending cancelled");
                }
            }
        }

        private void _buttonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";

            if (saveFileDialog.ShowDialog() == true)
                File.WriteAllText(saveFileDialog.FileName,_textBoxOutput.Text);
        }

        private void _buttonClear_Click(object sender, RoutedEventArgs e)
        {
            ClearOutput();
        }

        #endregion

        #region methods

        private void AddOutputText(string text, bool newLine = true)
        {
            if (newLine)
            {
                _textBoxOutput.Text = _textBoxOutput.Text + Environment.NewLine + text;
            }
            else
            {
                _textBoxOutput.Text = _textBoxOutput.Text + text;
            }
        }

        private void ClearOutput()
        {
            _textBoxOutput.Text = string.Empty;
        }

        public async Task<bool> SearchForDevices()
        {
            AddOutputText("Searching for devices...");

            Devices = await Broadlink.Discover(1);

            AddOutputText($"Found {Devices.Length} device(s).");

            if (Devices.Length > 0)
            {
                _comboBoxDevices.Items.Clear();

                for (int index = 0; index < Devices.Length; index++)
                {
                    var device = Devices[index];
                    string mac = BitConverter.ToString(device.Mac);
                    string id = BitConverter.ToString(device.Mac);
                    AddOutputText($"Device {index + 1}:   DeviceType:'{device.DeviceType}'   Host:'{device.Host}'   MAC:'{mac}'");

                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = $"Device {index + 1}";
                    item.ToolTip = $"Device {index + 1}:   DeviceType:'{device.DeviceType}'   Host:'{device.Host}'   MAC:'{mac}'";
                    //item.Tag = device;
                    _comboBoxDevices.Items.Add(item);
                }

                _comboBoxDevices.SelectedIndex = 0;
            }

            return true;
        }

        public async Task<bool> LearnIr()
        {
            AddOutputText(string.Empty);

            var device = Devices[_comboBoxDevices.SelectedIndex];

            if (device.DeviceType != DeviceType.Rm2Pro &&
                device.DeviceType != DeviceType.Rm)
            {
                AddOutputText($"IR is only supported by DeviceType 'Rm' and 'Rm2Pro', but the selected device is '{device.DeviceType}'");
                return false;
            }

            var rm = (Rm)device;

            if (!await rm.Auth())
            {
                AddOutputText("Auth Failure");
                return false;
            }

            AddOutputText("Starting IR frequency learning - press and hold the remote button...");

            var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            byte[] command = null;
            try
            {
                command = await rm.LearnIRCommnad(cancellationSource.Token);

                if (command == null || command.Length == 0)
                {
                    AddOutputText("Failed to learn IR command");
                    return false;
                }
            }
            catch (TaskCanceledException)
            {
                AddOutputText("Command learning cancelled");
                return false;
            }

            AddOutputText($"IR Command learned [{command.Length} bytes]");
            AddOutputText($"RawData : {command.ByteToHex()}");
            AddOutputText($"RawData Base64 : {command.ToBase64()}");

            if (LatestCommand != null &&
                LatestCommand.ByteToHex() == command.ByteToHex())
            {
                Console.WriteLine("Last command is the same as the new command!");
            }
            LatestCommand = command;
            LatestCommandType = CommandType.IR;

            return true;
        }

        public async Task<bool> LearnRf()
        {
            AddOutputText(string.Empty);

            var device = Devices[_comboBoxDevices.SelectedIndex];

            if (device.DeviceType != DeviceType.Rm2Pro)
            {
                AddOutputText($"RF is only supported by DeviceType 'Rm2Pro', but the selected device is '{device.DeviceType}'");
                return false;
            }

            var rm = (Rm2Pro)device;

            if (!await rm.Auth())
            {
                AddOutputText("Auth Failure");
                return false;
            }

            AddOutputText("Starting RF frequency learning - press and hold the remote button...");

            var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            byte[] command = null;
            try
            {
                command = await rm.LearnRfCommand(cancellationSource.Token, () =>
                {
                    //AddOutputText(".", false);
                    Console.Write('.');
                });

                if (command == null || command.Length == 0)
                {
                    AddOutputText("Failed to learn RF command");
                    return false;
                }
            }
            catch (TaskCanceledException)
            {
                AddOutputText("Command learning cancelled");
                return false;
            }

            AddOutputText($"RF Command learned [{command.Length} bytes]");
            AddOutputText($"RawData : {command.ByteToHex()}");
            AddOutputText($"RawData Base64 : {command.ToBase64()}");

            if(LatestCommand != null &&
               LatestCommand.ByteToHex() == command.ByteToHex())
            {
                Console.WriteLine("Last command is the same as the new command!");
            }
            LatestCommand = command;
            LatestCommandType = CommandType.RF;

            return true;
        }

        public async Task<bool> Send()
        {
            AddOutputText(string.Empty);

            var device = Devices[_comboBoxDevices.SelectedIndex];

            if (LatestCommandType == CommandType.IR)
            {
                if (device.DeviceType != DeviceType.Rm2Pro &&
                device.DeviceType != DeviceType.Rm)
                {
                    AddOutputText($"IR is only supported by DeviceType 'Rm' and 'Rm2Pro', but the selected device is '{device.DeviceType}'");
                    return false;
                }

                var rm = (Rm)device;

                if (!await rm.Auth())
                {
                    AddOutputText("Auth Failure");
                    return false;
                }

                AddOutputText("Sending IR command... ");
                await rm.SendData(LatestCommand);
                AddOutputText("IR command sent");
            }
            else if (LatestCommandType == CommandType.RF)
            {
                if (device.DeviceType != DeviceType.Rm2Pro)
                {
                    AddOutputText($"RF is only supported by DeviceType 'Rm2Pro', but the selected device is '{device.DeviceType}'");
                    return false;
                }

                var rm = (Rm2Pro)device;

                if (!await rm.Auth())
                {
                    AddOutputText("Auth Failure");
                    return false;
                }

                AddOutputText("Sending RF command... ");
                await rm.SendRfData(LatestCommand);
                AddOutputText("RF command sent");
            }


            return true;
        }


        #endregion
    }

    static class ByteExtensions
    {
        public static string ByteToHex(this byte[] bytes)
        {
            char[] array = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int num = bytes[i] >> 4;
                array[i * 2] = (char)(55 + num + ((num - 10 >> 31) & -7));
                num = (bytes[i] & 0xF);
                array[i * 2 + 1] = (char)(55 + num + ((num - 10 >> 31) & -7));
            }
            return new string(array);
        }

        public static string ToBase64(this byte[] data)
        {
            return Convert.ToBase64String(data, 0, data.Length);
        }
    }
}
