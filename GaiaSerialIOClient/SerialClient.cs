using System;
using System.Text;
using StackExchange.Redis;

namespace Gaia.SerialIO
{
    /// <summary>
    /// Client for serial port input and output.
    /// </summary>
    public class SerialClient
    {
        /// <summary>
        /// Channel connection to the Redis server.
        /// </summary>
        private readonly ISubscriber Communicator;
        /// <summary>
        /// Bound device name.
        /// </summary>
        private readonly string DeviceName;
        
        /// <summary>
        /// Try to connect to the remote device.
        /// </summary>
        /// <param name="device_name"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        public SerialClient(string device_name, uint port = 6379, string ip = "127.0.0.1")
        {
            DeviceName = device_name;
            var connection = ConnectionMultiplexer.Connect($"{ip}:{port.ToString()}");
            Communicator = connection.GetSubscriber();

            var database = connection.GetDatabase();
            if (!database.SetContains("serial_ports", DeviceName))
            {
                throw new Exception($"Target serial port device {DeviceName} is unavailable.");
            }

            Communicator.Subscribe($"serial_ports/{DeviceName}/read", (channel, value) =>
            {
                if (OnReceive != null)
                {
                    OnReceive(Encoding.ASCII.GetBytes(value.ToString()));
                }
            });
        }
        
        /// <summary>
        /// Delegate of bytes data handler.
        /// </summary>
        public delegate void BytesHandler(byte[] data);

        /// <summary>
        /// Receive event, triggered when receive data from serial ports.
        /// </summary>
        public event BytesHandler OnReceive;

        /// <summary>
        /// Send a string text to the serial port.
        /// </summary>
        /// <param name="text"></param>
        public void Send(string text)
        {
            Communicator.Publish($"serial_ports/{DeviceName}/write", text);
        }
        
        /// <summary>
        /// Send bytes to the serial port.
        /// </summary>
        /// <param name="bytes"></param>
        public void Send(byte[] bytes)
        {
            Communicator.Publish($"serial_ports/{DeviceName}/write", 
                Encoding.ASCII.GetString(bytes));
        }

        /// <summary>
        /// Send a command text to the command channel of the serial device router.
        /// </summary>
        /// <param name="command">Command text.</param>
        public void SendCommand(string command)
        {
            Communicator.Publish($"serial_ports/{DeviceName}/command", command);
        }
    }
}