using System;
using System.Text;
using StackExchange.Redis;

using Gaia.ConfigurationService;
using Gaia.LogService;
using ServiceStack;

namespace Gaia.SerialIO
{
    public class Router : IDisposable
    {
        public bool LifeFlag { private set; get; }
        
        private readonly ISubscriber Subscriber;
        private readonly System.IO.Ports.SerialPort Port;

        private readonly string DeviceName;
        
        class Settings
        {
            public int BaudRate = 115200;
            public int DataBits = 8;
            public System.IO.Ports.Parity Parity = System.IO.Ports.Parity.None;
            public System.IO.Ports.StopBits StopBits = System.IO.Ports.StopBits.One;
            public System.IO.Ports.Handshake Handshake = System.IO.Ports.Handshake.None;

            public void SetParity(string parity)
            {
                switch (parity)
                {
                    case "None":
                        Parity = System.IO.Ports.Parity.None;
                        break;
                    case "Even":
                        Parity = System.IO.Ports.Parity.Even;
                        break;
                    case "Odd":
                        Parity = System.IO.Ports.Parity.Odd;
                        break;
                    case "Space":
                        Parity = System.IO.Ports.Parity.Space;
                        break;
                    case "Mark":
                        Parity = System.IO.Ports.Parity.Mark;
                        break;
                }
            }
            public void SetStopBits(string stop_bits)
            {
                switch (stop_bits)
                {
                    case "One":
                        StopBits = System.IO.Ports.StopBits.One;
                        break;
                    case "Two":
                        StopBits = System.IO.Ports.StopBits.Two;
                        break;
                    case "OnePointFive":
                        StopBits = System.IO.Ports.StopBits.OnePointFive;
                        break;
                    case "None":
                        StopBits = System.IO.Ports.StopBits.None;
                        break;
                }
            }
            public void SetHandshake(string handshake)
            {
                switch (handshake)
                {
                    case "None":
                        Handshake = System.IO.Ports.Handshake.None;
                        break;
                    case "XOnXOff":
                        Handshake = System.IO.Ports.Handshake.XOnXOff;
                        break;
                    case "RequestToSend":
                        Handshake = System.IO.Ports.Handshake.RequestToSend;
                        break;
                    case "RequestToSendXOnXOff":
                        Handshake = System.IO.Ports.Handshake.RequestToSendXOnXOff;
                        break;
                }
            }

            public string GetParity()
            {
                var parity = Parity switch
                {
                    System.IO.Ports.Parity.None => "None",
                    System.IO.Ports.Parity.Even => "Even",
                    System.IO.Ports.Parity.Odd => "Odd",
                    System.IO.Ports.Parity.Space => "Space",
                    System.IO.Ports.Parity.Mark => "Mark",
                    _ => ""
                };
                return parity;
            }
            public string GetStopBits()
            {
                var stop_bits = StopBits switch
                {
                    System.IO.Ports.StopBits.None => "None",
                    System.IO.Ports.StopBits.One => "One",
                    System.IO.Ports.StopBits.Two => "Two",
                    System.IO.Ports.StopBits.OnePointFive => "OnePointFive",
                    _ => ""
                };
                return stop_bits;
            }
            public string GetHandshake()
            {
                var handshake = Handshake switch
                {
                    System.IO.Ports.Handshake.None => "None",
                    System.IO.Ports.Handshake.RequestToSend => "RequestToSend",
                    System.IO.Ports.Handshake.XOnXOff => "XOnXOff",
                    System.IO.Ports.Handshake.RequestToSendXOnXOff => "RequestToSendXOnXOff",
                    _ => ""
                };
                return handshake;
            }
        }
        
        private readonly LogClient Logger;

        /// <summary>
        /// Construct and bind the given configuration file.
        /// </summary>
        /// <param name="device_name">Name of the serial port device file.</param>
        /// <param name="port">Port of the Redis server.</param>
        /// <param name="ip">IP address of the Redis server.</param>
        /// <exception cref="Exception">When failed to connect to the redis server.</exception>
        public Router(string device_name = "ttyUSB", uint port = 6379, string ip = "127.0.0.1")
        {
            DeviceName = device_name;
            var configurator = new ConfigurationClient(device_name, port, ip);
            Logger = new LogClient(port, ip)
            {
                Author = "SerialPort_" + device_name,
                PrintToConsole = true
            };

            var configuration = new Settings
            {
                DataBits = Convert.ToInt32(configurator.Get("data_bits", "8")),
                BaudRate = Convert.ToInt32(configurator.Get("baud_rate", "115200"))
            };

            configuration.SetParity(configurator.Get("parity", "None"));
            configuration.SetStopBits(configurator.Get("stop_bits", "One"));
            configuration.SetHandshake(configurator.Get("handshake", "None"));
            
            Port = new System.IO.Ports.SerialPort("/dev/" + device_name)
            {
                Encoding = Encoding.Latin1,
                Parity = configuration.Parity,
                BaudRate = configuration.BaudRate,
                DataBits = configuration.DataBits,
                StopBits = configuration.StopBits,
                Handshake = configuration.Handshake
            };
            Port.Open();
            Port.DataReceived += delegate(object sender, System.IO.Ports.SerialDataReceivedEventArgs args)
            {
                var buffer = new byte[Port.BytesToRead];
                Port.Read(buffer, 0, buffer.Length);
                HandleInputData(buffer);
            };
            
            Logger.RecordMilestone(
                "Serial port device opened, " +
                $"baud rate: {configuration.BaudRate.ToString()}, data bits: {configuration.DataBits.ToString()}, " +
                $"parity: {configuration.GetParity()}, stop bits: {configuration.GetStopBits()}.");
            
            var connection = ConnectionMultiplexer.Connect($"{ip}:{port.ToString()}");
            
            Subscriber = connection.GetSubscriber();
            Subscriber.Subscribe($"serial_ports/{device_name}/write", (channel, value) =>
            {
                HandleOutputData(value.ConvertTo<byte[]>());
            });
            Subscriber.Subscribe($"serial_ports/{device_name}/command", (channel, value) =>
            {
                HandleCommand(value);
            });
            
            Logger.RecordMilestone(
                $"Serial port device {DeviceName} online, Redis server on {ip}:{port.ToString()} connected.");
            
            LifeFlag = true;
        }

        /// <summary>
        /// Close the serial port device when destruct.
        /// </summary>
        public void Dispose()
        {
            if (Port.IsOpen)
            {
                Port.Close();
            }
        }

        /// <summary>
        /// Bridge the input serial port data into Redis channel.
        /// </summary>
        /// <param name="bytes">Transferred data.</param>
        private void HandleInputData(byte[] bytes)
        {   
            if (Subscriber.IsConnected())
            {
                Subscriber.Publish($"serial_ports/{DeviceName}/read", bytes);
            }
        }
        
        /// <summary>
        /// Bridge the output message into the serial_ports port device.
        /// The message will be encoded in ASCII bytes.
        /// </summary>
        /// <param name="message">Message text to send into the serial_ports port.</param>
        private void HandleOutputData(byte[] message)
        {
            if (Port.IsOpen)
            {
                Port.Write(message, 0, message.Length);
            }
        }

        private void HandleCommand(string command)
        {
            switch (command)
            {
                case "shutdown":
                    LifeFlag = false;
                    Logger.RecordMilestone("Shutdown command received.");
                    Subscriber.UnsubscribeAll();
                    Port.Close();
                    break;
            }
        }
    }
}