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

*   `EnableRegionalProcessing` (boolean): Enables or disables regional processing. When enabled, the server only processes regions of the map that are active (near players), significantly improving performance in large worlds. Default: `false`.

#### Regional Processing (`Performance.RegionalProcessing`)

These settings are only active if `EnableRegionalProcessing` is set to `true`.

*   `RegionSize` (integer): The size of a single region in chunks. For example, a value of 8 means each region is 8x8 chunks. Default: `8`.
*   `MaxThreads` (integer): The maximum number of threads to use for processing regions in parallel. If set to `0`, the number of threads will be determined automatically. Default: `0`.
*   `ActivationRange` (integer): The distance, in regions, around a player that is considered "active". For example, a value of 1 means a 3x3 grid of regions around the player is active. Default: `1`.
*   `ZActivationRange` (integer): The activation range for Z-levels (height). A value of 0 means only the player's current Z-level is activated. A value of 1 would activate the level above and below as well. Default: `0`.
*   `EnableRegionMerging` (boolean): Enables the merging of adjacent active regions into a single processing unit, which can improve performance by reducing overhead. Default: `false`.
*   `MinRegionsToMerge` (integer): The minimum number of adjacent active regions required to form a merged region. Default: `2`.
*   `ScriptActiveRegionTimeout` (integer): The time in seconds that a region activated by a script (not a player) will remain active before being automatically deactivated. Default: `60`.

#### Time Budgeting (`TimeBudgeting.ScriptHost`)

*   `Enabled` (boolean): Enables or disables time budgeting for the script host. Default: `true`.
*   `BudgetPercent` (float): The percentage of each tick's time that can be used for script execution. For example, a value of `0.5` means 50% of the tick time. Default: `0.5`.

### Development Settings (`Development`)

*   `ScriptReloadDebounceMs` (integer): The delay in milliseconds before reloading scripts after a file change is detected. This prevents multiple reloads from occurring in rapid succession. Default: `200`.
