# Scripting

The BYOND 2.0 game engine features a polyglot scripting environment. While **DM (Dream Maker)** is the primary language, **Lua** and **C#** are also supported for specialized use cases.

## Language Recommendations

*   **DM:** Recommended for all general game logic. Its syntax is tailored for defining game objects and interactions.
*   **Lua:** Ideal for dynamic scenarios, rapid prototyping, or implementing complex AI behaviors that benefit from its lightweight nature.
*   **C#:** Best for performance-critical systems, complex data processing, or integrating with external .NET libraries.

## DM (Dream Maker)

DM is the native language of the BYOND platform. The engine uses the OpenDream compiler to transform `.dm` files into bytecode executed by the `DreamVM`.

*   **Execution:** Executed by the high-performance `DreamVM`, a stack-based virtual machine.
*   **Entry Point:** The server automatically executes the `/world/New()` procedure when the game world is created.

### DM Example

This example shows a player object that logs a message to the world log when it takes damage, demonstrating interaction with a core API.

```dm
// scripts/player.dm
/obj/creature/player
    var/hp = 100

    proc/TakeDamage(amount)
        hp -= amount
        world.log << "[src] took [amount] damage! [hp] HP remaining."
        if (hp <= 0)
            del src
```

## Lua

Lua is a lightweight and flexible scripting language integrated via the NLua library.

*   **API Access:** Lua scripts can access engine APIs through a global `GameApi` object.
*   **Entry Point:** The server executes the `main()` function in the `scripts/main.lua` file on startup.

### Lua API Example

This example demonstrates a more advanced task: finding all player objects and giving them a "healing potion".

```lua
-- scripts/main.lua
function main()
    local objectApi = global.GameApi.ObjectApi
    local players = objectApi:GetObjects("/obj/creature/player")

    for i, player in ipairs(players) do
        local potion = objectApi:CreateObject("/obj/item/potion/healing")
        potion:SetProperty("x", player:GetProperty("x"))
        potion:SetProperty("y", player:GetProperty("y"))
        print("Gave a healing potion to player "..tostring(player.Id))
    end
end
```

## C#

C# scripting offers the highest performance by leveraging Roslyn for on-the-fly compilation.

*   **API Access:** C# script entry points receive an `IGameApi` instance.
*   **Entry Point:** The server looks for a static `Main(IGameApi gameApi)` method in any class within `.cs` files in the `scripts` directory.

### C# API Example

This example showcases a performance-oriented task: calculating the average health of all players using LINQ from the .NET library.

```csharp
// scripts/health_checker.cs
using System.Linq;
using BYOND2.Core.Api;
using BYOND2.Shared.GameObjects;

public class HealthChecker
{
    public static void Main(IGameApi gameApi)
    {
        var players = gameApi.ObjectApi.GetObjects("/obj/creature/player");
        if (!players.Any()) return;

        double averageHp = players.Average(p => (double)p.GetProperty("hp"));

        gameApi.WorldApi.Log($"Average player HP: {averageHp:F2}");
    }
}
```

## Hot-Reloading

All three scripting systems support hot-reloading. The server monitors files in the `scripts` directory for changes and automatically reloads them, allowing for real-time iteration on game logic without restarting the server.
