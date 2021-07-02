#pragma once

#include <string>
#include <memory>
#include <sw/redis++/redis++.h>
#include <GaiaEvents/GaiaEvents.hpp>
#include <GaiaBackground/GaiaBackground.hpp>

namespace Gaia::SerialIO
{
    class SerialClient
    {
    private:
        const std::string ChannelWriteName;
        const std::string ChannelReadName;
        const std::string ChannelCommandName;

        std::shared_ptr<sw::redis::Redis> Connection;
        std::unique_ptr<sw::redis::Subscriber> Subscriber;

    public:
        /**
         * @brief Constructor which will bind the device name,
         *        and try to make a connection to the Redis server on the given address.
         * @param device_name Name of the serial port device to bind.
         * @param port Port of the Redis server.
         * @param ip IP address of the Redis server.
         */
        explicit SerialClient(const std::string& device_name,
                              unsigned int port = 6379, const std::string& ip = "127.0.0.1");
        /**
         * @brief Constructor which will bind the device name, and reuse the connection to the Redis server.
         * @param device_name Name of the serial port device to bind.
         * @param connection Connection to the Redis server.
         */
        explicit SerialClient(const std::string& device_name, std::shared_ptr<sw::redis::Redis> connection);

        /// Triggered when receive data from the serial port device.
        Events::Event<std::string> OnReceive;

        /// Send a text to the serial port.
        void Send(const std::string& text);
        /// Send a command to the command channel of the serial port.
        void SendCommand(const std::string& command);

        /// Consume serial port messages for once.
        void Listen();

        /// Background listener.
        Background::BackgroundWorker BackgroundListener;
        /**
         * @brief The interval time between two listen attempts for the background listener.
         * @details Default value is 10ms.
         */
        std::atomic<std::chrono::steady_clock::duration> BackgroundListenIntervalTime;
    };
}
