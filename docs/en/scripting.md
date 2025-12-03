# Scripting

The BYOND 2.0 game engine supports both Lua and C# for scripting. This allows developers to choose the best tool for the job, whether it's the simplicity and dynamic nature of Lua or the performance and strong typing of C#.

## Lua Engine

Integration with Lua is achieved using the NLua library, which provides a bridge between C# and Lua. This allows for calling C# functions from Lua and vice versa.

### Lua API Example

Here is an example of how to interact with the game API from a Lua script:

```lua
-- scripts/main.lua
local mapApi = global.GameApi.MapApi
local objectApi = global.GameApi.ObjectApi

-- Load a map
mapApi:LoadMap("maps/example.dmm")

-- Create a new object
local player = objectApi:CreateObject("/obj/creature/player")
player:SetProperty("x", 10)
player:SetProperty("y", 5)
```

## C# Engine

C# scripting is supported via Roslyn, allowing for on-the-fly compilation of C# code. This provides a powerful way to extend the engine with high-performance, statically-typed code.

### C# API Example

Here is the same example, but implemented as a C# script:

```csharp
// scripts/main.cs
using BYOND2.Core.Api;

public class MainScript
{
    public static void Main(IGameApi gameApi)
    {
        // Load a map
        gameApi.MapApi.LoadMap("maps/example.dmm");

        // Create a new object
        var player = gameApi.ObjectApi.CreateObject("/obj/creature/player");
        player.SetProperty("x", 10);
        player.SetProperty("y", 5);
    }
}
```

## Hot-Reloading

One of the key features of the engine is the hot-reloading of scripts. The server monitors files in the `scripts` directory for changes and automatically reloads them when changes are detected. This significantly speeds up the development process, as changes to the game logic can be seen in real-time without restarting the server.

The hot-reloading mechanism is implemented using `FileSystemWatcher` and includes debouncing with a `System.Threading.Timer` to prevent multiple reloads from occurring in rapid succession. This feature works for both Lua and C# scripts.

## Entry Points

The main entry point for Lua scripts is the `scripts/main.lua` file. For C# scripts, the entry point is a static `Main` method in a class within a `.cs` file in the `scripts` directory. When the server starts or after a hot-reload, these entry points are executed. They should contain the main logic for initializing the game world and other important components.
