# Server Guide

This guide provides instructions on how to run the BYOND 2.0 server and configure it using the `server_config.json` file.

## How to Run the Server

To run the game server, execute the following command from the root of the project:

```bash
dotnet run --project Server/Server.csproj
```

The server will start using the settings defined in `server_config.json`. If this file does not exist, it will be created with default values.

## Server Configuration (`server_config.json`)

The server's behavior can be customized through the `server_config.json` file. Below is a detailed description of each available setting.

### General Settings

*   `ServerName` (string): The name of the server that will be displayed to players. Default: `"BYOND 2.0 Server"`.
*   `ServerDescription` (string): A brief description of the server. Default: `"A default server instance."`.
*   `MaxPlayers` (integer): The maximum number of players that can connect to the server simultaneously. Default: `32`.
*   `EnableVm` (boolean): Enables or disables the DreamVM for running DM scripts. Default: `false`.
*   `VmMaxInstructions` (integer): The maximum number of instructions a single VM thread can execute before being paused. This is a safeguard against infinite loops. Default: `1000000`.

### Network Settings (`Network`)

*   `Mode` (string): The network mode. Can be `Automatic` or `Manual`. Default: `Automatic`.
*   `IpAddress` (string): The IP address the server will bind to. Default: `"127.0.0.1"`.
*   `UdpPort` (integer): The UDP port the server will listen on. Default: `9050`.
*   `ConnectionKey` (string): A secret key used to validate client connections. Default: `"BYOND2.0"`.
*   `DisconnectTimeout` (integer): The time in milliseconds a client can be unresponsive before being disconnected. Default: `10000`.

### Threading Settings (`Threading`)

*   `Mode` (string): The threading mode. Can be `Automatic` or `Manual`. Default: `Automatic`.
*   `ThreadCount` (integer): The number of threads to use for the server. If set to `0`, the number of threads will be determined automatically based on the system's capabilities. Default: `0`.

### Performance Settings (`Performance`)

*   `TickRate` (integer): The number of game ticks per second. Default: `60`.
*   `VmInstructionSlice` (integer): The number of instructions a VM thread executes in one go before yielding to other threads. Default: `100`.
*   `SnapshotBroadcastInterval` (integer): The interval in milliseconds at which game state snapshots are sent to clients. Default: `100`.

#### Time Budgeting (`TimeBudgeting.ScriptHost`)

*   `Enabled` (boolean): Enables or disables time budgeting for the script host. Default: `true`.
*   `BudgetPercent` (float): The percentage of each tick's time that can be used for script execution. For example, a value of `0.5` means 50% of the tick time. Default: `0.5`.

### Development Settings (`Development`)

*   `ScriptReloadDebounceMs` (integer): The delay in milliseconds before reloading scripts after a file change is detected. This prevents multiple reloads from occurring in rapid succession. Default: `200`.
