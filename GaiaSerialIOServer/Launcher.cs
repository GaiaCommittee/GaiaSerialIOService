using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Gaia.LogService;
using Gaia.NameService;
using Microsoft.Extensions.CommandLineUtils;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Gaia.SerialIO
{
    public class Launcher
    {
        /// <summary>
        /// Entrance point.
        /// </summary>
        /// <param name="arguments">Arguments from the command line.</param>
        static void Main(string[] arguments)
        {
            // Prepare command line arguments parser.
            var application = new CommandLineApplication();
            var option_host = application.Option("-h | --host <address>", 
                "set the ip address of the Redis server to connect.",
                CommandOptionType.SingleValue);
            var option_port = application.Option("-p | --port <number>", 
                "set the port number of the Redis server to connect.",
                CommandOptionType.SingleValue);
            var option_devices = application.Option("-d | --device <name1,name2,...>",
                "name of the devices to route.",
                CommandOptionType.MultipleValue);
            application.HelpOption("-? | --help");
            
            application.OnExecute(() =>
            {
                var crashed = false;
                // Loop until launcher normally exited.
                do
                {
                    try
                    {
                        var launcher = new Launcher();
                        crashed = false;
                        Console.WriteLine("Launching configuration service...");
                        launcher.Launch(
                            option_devices.HasValue() ? option_devices.Values : new List<string>{"ttyUSB"},
                            option_port.HasValue() ? Convert.ToUInt32(option_port.Value()) : 6379,
                            option_host.HasValue() ? option_host.Value() : "127.0.0.1"
                        );
                    }
                    catch (Exception error)
                    {
                        crashed = true;
                        Console.WriteLine(error.Message);
                        Console.WriteLine("Configuration service crashed. Restart in 1 seconds.");
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                } while (crashed);

                return 0;
            });

            // Parse command line arguments and then perform the action.
            application.Execute(arguments);
        }
        
        private LogClient Logger;
        private NameClient ServiceNameResolver;
        private NameToken ServiceNameToken;
        
        /// <summary>
        /// Launch routers depends on the device names given in the command line arguments.
        /// </summary>
        /// <param name="devices">Names of devices to route.</param>
        /// <param name="port">Port of the Redis server.</param>
        /// <param name="ip">IP address of the Redis server.</param>
        private void Launch(IReadOnlyCollection<string> devices, uint port = 6379, string ip = "127.0.0.1")
        {
            var connection = ConnectionMultiplexer.Connect($"{ip}:{port.ToString()}");
            var database = connection.GetDatabase();
            
            ServiceNameResolver = new NameClient();
            Logger = new LogClient();
            Logger.RecordMilestone("IO Service initiating...");

            var routers = new List<Router>();

            foreach (var device_name in devices)
            {
                Logger.RecordMilestone($"Try to start router on /dev/{device_name} ...");
                routers.Add(new Router(device_name, port, ip));
                database.SetAdd("serial_ports", device_name);
            }

            ServiceNameToken = ServiceNameResolver.HoldName("SerialIOService");
            
            // Check the life flag of the router in every second. 
            while (routers.Any(router => router.LifeFlag))
            {
                ServiceNameToken.Update();
                Thread.Sleep(1000);
            }
            
            foreach (var device_name in devices)
            {
                database.SetRemove("serial_ports", device_name);
            }
            
            Logger.RecordMilestone("IO Service stopped.");
        }
    }
}