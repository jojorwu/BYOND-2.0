# Server Guide

This guide provides instructions on how to run the BYOND 2.0 server and configure it using the `server_config.json` file.

## How to Run the Server

The easiest way to run the server is by using the provided shell script from the project root:

```bash
./run_server.sh
```

The server will start using the settings defined in `server_config.json`. If this file does not exist, it will be created with default values upon the first run.

## Server Configuration (`server_config.json`)

The server's behavior can be customized through the `server_config.json` file. Below is a detailed description of each available setting.

### General Settings

*   `ServerName` (string): The name of the server that will be displayed to players. Default: `"BYOND 2.0 Server"`.
*   `ServerDescription` (string): A brief description of the server. Default: `"A default server instance."`.
*   `MaxPlayers` (integer): The maximum number of players that can connect to the server simultaneously. Default: `32`.
*   `EnableVm` (boolean): Enables or disables the DreamVM for running DM scripts. **Important:** This must be set to `true` to run any DM game logic. Default: `true`.
*   `VmMaxInstructions` (integer): The maximum number of instructions a single VM thread can execute before being paused. This is a safeguard against infinite loops. Default: `1000000`.
*   `PlayerObjectTypePath` (string): The object type path for the player object that is automatically created when a new client connects. Default: `"/obj/player"`.

### Network Settings (`Network`)

*   `Mode` (string): Not currently used. Reserved for future networking models. Default: `"Automatic"`.
*   `IpAddress` (string): The IP address the server will bind to. Default: `"127.0.0.1"`.
*   `UdpPort` (integer): The UDP port for real-time game traffic. Default: `9050`.
*   `ConnectionKey` (string): A secret key used to validate client connections. Default: `"BYOND2.0"`.
*   `DisconnectTimeout` (integer): The time in milliseconds a client can be unresponsive before being disconnected. Default: `10000`.

### HTTP Server Settings (`HttpServer`)

This section configures the built-in web server used for serving game assets.

*   `Enabled` (boolean): Enables or disables the HTTP asset server. Default: `true`.
*   `Port` (integer): The TCP port the HTTP server will listen on. Default: `9051`.
*   `AssetsRoot` (string): The path to the directory containing assets to be served. Relative to the project root. Default: `"assets"`.

### Performance Settings (`Performance`)

*   `TickRate` (integer): The number of game ticks per second. Default: `60`.
*   `EnableRegionalProcessing` (boolean): Enables or disables the high-performance [Regional Processing](./regional-processing.md) system. Default: `false`.
*   `VmInstructionSlice` (integer): The number of instructions a VM thread executes in one go before yielding to other threads. Default: `100`.
*   `SnapshotBroadcastInterval` (integer): The interval in milliseconds at which game state snapshots are sent to clients. Default: `100`.

#### Regional Processing Settings (`Performance.RegionalProcessing`)

These settings are only active if `EnableRegionalProcessing` is `true`.

*   `ActivationRange` (integer): The radius (in regions) around a player that will be activated. A value of `1` activates a 3x3 area. Default: `1`.
*   `ZActivationRange` (integer): The number of Z-levels above and below a player that will be activated. `0` means only the current Z-level. Default: `0`.
*   `EnableRegionMerging` (boolean): Enables or disables the merging of adjacent active regions for more efficient processing. Default: `false`.
*   `MinRegionsToMerge` (integer): The minimum number of adjacent active regions required to form a merged group. Default: `2`.

#### Time Budgeting (`Performance.TimeBudgeting.ScriptHost`)

*   `Enabled` (boolean): Enables or disables time budgeting for the script host. Default: `true`.
*   `BudgetPercent` (float): The percentage of each tick's time (0.0 to 1.0) that can be used for script execution. Default: `0.5` (50%).

### Development Settings (`Development`)

*   `ScriptReloadDebounceMs` (integer): The delay in milliseconds before reloading scripts after a file change is detected. This prevents multiple reloads from occurring in rapid succession. Default: `200`.
