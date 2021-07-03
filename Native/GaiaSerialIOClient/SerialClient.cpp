#include "SerialClient.hpp"

#include <thread>

namespace Gaia::SerialIO
{
    /// Make a new connection to the Redis server.
    SerialClient::SerialClient(const std::string& device_name, unsigned int port, const std::string &ip) :
        SerialClient(device_name,
                     std::make_shared<sw::redis::Redis>("tcp://" + ip + ":" + std::to_string(port)))
    {}

    /// Reuse the connection to a Redis server.
    SerialClient::SerialClient(const std::string &device_name, std::shared_ptr<sw::redis::Redis> connection) :
            Connection(std::move(connection)),
            ChannelReadName("serial_ports/" + device_name + "/read"),
            ChannelWriteName("serial_ports/" + device_name + "/write"),
            ChannelCommandName("serial_ports/" + device_name + "/command"),
            BackgroundListenIntervalTime(std::chrono::milliseconds(10)),
            BackgroundListener([this](const auto& life_flag){
                while (life_flag)
                {
                    this->Listen();
                    std::this_thread::sleep_for(this->BackgroundListenIntervalTime.load());
                }
            })
    {
        Subscriber = std::make_unique<sw::redis::Subscriber>(Connection->subscriber());
        Subscriber->subscribe(ChannelReadName);
        Subscriber->on_message([this](const std::string& channel, std::string value)
                               {
                                   this->OnReceive.Trigger(std::move(value));
                               });
    }

    /// Send a text to the serial port.
    void SerialClient::Send(const std::string& text)
    {
        Connection->publish(ChannelWriteName, text);
    }

    /// Send a command to the command channel of the serial port.
    void SerialClient::SendCommand(const std::string& command)
    {
        Connection->publish(ChannelCommandName, command);
    }

    /// Consume serial port messages for once.
    void SerialClient::Listen()
    {
        if (Subscriber)
        {
            Subscriber->consume();
        }
    }
}