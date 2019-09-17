using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nbiot_test_client
{
    class Bridge
    {
        MainWindow main;

        int baudrate = 9600;
        Parity parity = Parity.None;
        int databits = 8;
        StopBits stopbits = StopBits.One;

        Thread thread;
        SerialPort serial;

        public Bridge(MainWindow main, string tty)
        {
            this.main = main;

            serial = new SerialPort(tty, baudrate, parity, databits, stopbits);
            serial.Open();

            thread = new Thread(new ThreadStart(Process));
            thread.Start();
        }

        public void Destroy()
        {
            serial.Close();
        }

        public void Write(byte[] bytes)
        {
            serial.Write(bytes, 0, bytes.Length);
        }

        void Process()
        {
            try
            {
                while (serial.IsOpen)
                {
                    byte[] bytes = new byte[250];
                    int s;
                    while ((s = serial.Read(bytes, 0, bytes.Length)) > 0)
                    {
                        main.Publish(bytes, 0, s);
                    }
                }
            }
            catch (Exception e)
            {
                // skip
            }
        }
    }
}
