# Scripting

The BYOND 2.0 game engine features a polyglot scripting environment, allowing developers to use multiple languages to define game logic. The primary supported languages are **DM (Dream Maker)**, **Lua**, and **C#**.

## DM (Dream Maker)

DM is the native language of the BYOND platform and is the recommended choice for most game logic in BYOND 2.0. The engine includes the OpenDream compiler, which compiles `.dme` and `.dm` files into bytecode that the `DreamVM` can execute.

*   **Execution:** DM code is executed by the high-performance `DreamVM`, a stack-based virtual machine.
*   **Entry Point:** The main entry point for all game logic is the `world.New()` procedure in your DM code.

### DM Example

```dm
// main.dm
/world/New()
    // Load a map
    world.log << "Loading map..."
    // Further initialization code

/obj/creature/player
    var/hp = 100

    proc/TakeDamage(amount)
        hp -= amount
        if (hp <= 0)
            del src
```

## Lua

Lua provides a lightweight and flexible scripting option. It is well-suited for quick prototyping or for specific tasks that benefit from its dynamic nature.

*   **Integration:** Lua is integrated via the NLua library, which provides a bridge to the .NET runtime.
*   **API Access:** Lua scripts can access the core engine APIs through a global `GameApi` object.
*   **Entry Point:** The entry point for Lua is the `scripts/main.lua` file.

### Lua API Example

```lua
-- scripts/main.lua
local mapApi = global.GameApi.MapApi
local objectApi = global.GameApi.ObjectApi

-- Create a new object
local player = objectApi:CreateObject("/obj/creature/player")
player:SetProperty("x", 10)
player:SetProperty("y", 5)
```

## C#

C# scripting offers the highest performance and the full power of the .NET ecosystem. It is ideal for performance-critical systems or for extending the engine with complex, statically-typed logic.

*   **Integration:** C# scripts are compiled on-the-fly using Roslyn.
*   **API Access:** C# scripts receive an `IGameApi` instance as an argument to their entry point.
*   **Entry Point:** The entry point for C# is a static `Main` method in any class within a `.cs` file in the `scripts` directory.

### C# API Example

```csharp
// scripts/main.cs
using BYOND2.Core.Api;

public class MainScript
{
    public static void Main(IGameApi gameApi)
    {
        // Create a new object
        var player = gameApi.ObjectApi.CreateObject("/obj/creature/player");
        player.SetProperty("x", 10);
        player.SetProperty("y", 5);
    }
}
```

## Hot-Reloading

All three scripting systems support hot-reloading. The server monitors files in the `scripts` and project directories for changes and automatically reloads them. This allows for real-time iteration on game logic without restarting the server.
