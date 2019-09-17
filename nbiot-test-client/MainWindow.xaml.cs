using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Media;

using Microsoft.VisualBasic;
using Newtonsoft.Json;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace nbiot_test_client
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        static string TOPIC_STATUS = "/maxlong/broker/imei/{0}/status";
        static string TOPIC_TX = "/maxlong/broker/imei/{0}/tx";
        static string TOPIC_RX = "/maxlong/broker/imei/{0}/rx";        
        
        string imei;

        MqttClient client;
        Bridge bridge;

        public MainWindow()
        {
            imei = Interaction.InputBox("NB-IoT Gateway's IMEI");
            if (String.IsNullOrEmpty(imei) || (imei.Length != 15))
            {
                MessageBox.Show("Exit by invalid IMEI - " + imei, "Warning");
                Close();

                return;
            }

            InitializeComponent();
            send.Click += Send_Click;
            clear.Click += Clear_Click;
            bind.Click += Bind_Click;

            gateway.Content = imei;

            foreach (string com in SerialPort.GetPortNames())
            {
                ttys.Items.Add(com);
            }

            init();           
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (bridge != null)
            {
                bridge.Destroy();
            }

            if (client != null)
            {
                client.Disconnect();
            }            
        }

        // ======

        void init()
        {
            client = new MqttClient(Properties.Settings.Default.host);
            client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            try
            {
                client.Connect(
                    Properties.Settings.Default.clientId,
                    Properties.Settings.Default.username,
                    Properties.Settings.Default.password);

                client.Subscribe(new string[]
                {
                    String.Format(TOPIC_STATUS, imei),
                    String.Format(TOPIC_RX, imei)

                }, new byte[]
                {
                    MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE,
                    MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE
                });
            }
            catch (Exception e)
            {
                AppendText("ERROR: Failed to connnect to " + Properties.Settings.Default.host);
                AppendText("Please re-try later");
            }
        }

        private void Client_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            if (e.Topic.EndsWith("status")) // /maxlong/broker/imei/{0}/status
            {
                string json = System.Text.Encoding.Default.GetString(e.Message);
                Dictionary<string, string> status = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                string type = status["type"];
                string timestamp = status["timestamp"];                

                if ("heartbeat".Equals(type)) // { "imei":"866425030027611","type":"heartbeat","timestamp":"2019-09-08T06:58:26.436Z","from":"211.77.241.100:12191","step":1,"rssi":-79,"locationAreaCode":10222,"cellId":14723,"accessTechnology":"Cat NB1"}
                {
                    string from = status["from"];
                    string step = status["step"];
                    string rssi = status["rssi"];

                    Println(String.Format("Heartbeat[#{0}] from {1}. RSSI is {2} dBm.", step, from, rssi));
                    SetStatus("on-line", (Color) ColorConverter.ConvertFromString("Green"));
                }
                else if ("disconnect".Equals(type)) // {"imei":"866425030027611","type":"disconnect","timestamp":"2019-09-08T07:15:48.767Z","from":"211.77.241.100:44791"}
                {
                    Println("Disconnected!");
                    SetStatus("off-line", (Color) ColorConverter.ConvertFromString("Red"));
                }
            }
            else if (e.Topic.EndsWith("rx")) // /maxlong/broker/imei/{0}/rx
            {
                Println("RECV - " + ByteArrayToString(e.Message));
                if (bridge != null)
                {
                    bridge.Write(e.Message);
                }
            }
        }

        // used by non-UI thread
        void Println(string text)
        {
            Dispatcher.BeginInvoke((Action)(() => AppendText(text)));
        }

        void AppendText(string text)
        {
            output.AppendText(String.Format("[{0}] {1} \r\n", DateTime.Now.ToString("HH:mm:ss"), text));
            output.Focus();
            output.Select(output.Text.Length - 1, output.Text.Length);
            output.ScrollToEnd();            
        }

        // used by non-UI thread
        void SetStatus(string text, Color color)
        {
            Dispatcher.BeginInvoke((Action)(() => {
                status.Content = text;
                status.Foreground = new SolidColorBrush(color);
            }));
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string hex = tx.Text.Replace(" ", "");
                byte[] bytes = StringToByteArray(hex);
                Publish(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                AppendText("ERROR - incorrect HEX input: " + ex.Message);
            }            
        }

        private void Bind_Click(object sender, RoutedEventArgs e)
        {
            string tty = (string) ttys.SelectedItem;
            if (String.IsNullOrEmpty(tty))
            {
                MessageBox.Show("You must choice a serial port!");
                return;
            }

            try
            {
                bridge = new Bridge(this, tty);

                ttys.IsEnabled = false; // you cannot change it again
                bind.IsEnabled = false; // you cannot change it again            

                AppendText(String.Format("Bridge {0} to NB-IoT Gateway", tty));
            }
            catch (Exception ex)
            {
                AppendText("ERROR - failed to open serial port - " + tty + ", " + ex.Message);
            }
        }

        public void Publish(byte[] bytes, int offset, int count)
        {
            byte[] payload = new byte[count];
            Array.Copy(bytes, offset, payload, 0, count);

            string topic = String.Format(TOPIC_TX, imei);
            client.Publish(topic, payload);

            Println("SEND - " + ByteArrayToString(payload));
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string ByteArrayToString(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            output.Text = "";
        }
    }
}
