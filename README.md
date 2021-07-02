# GaiaSerialIOService
Serial input and output service, included in Gaia platform.

## Redis Information

All routed devices are registered in set "serial_ports".

A router will use the configuration under "SerialPort_*DeviceName*",
items are: 
integer "data_bits", 
integer "baud_rate", 
enum "parity" (from "None", "Odd", "Even", "Space", "Mark"),
enum "stop_bits" (from "None", "One", "Two", "OnePointFive"),
enum "handshake" (from "None", "RequestToSend", "XOnXOff", "RequestToSendXOnXOff").
Messages can be routed through channels "serial_ports/*DeviceName*/read" and "serial_ports/*DeviceName*/write",
and commands will be processed through channel "serial_ports/*DeviceName*/command".
