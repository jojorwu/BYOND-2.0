# Scripting

The BYOND 2.0 game engine uses Lua as its scripting language. This allows developers to quickly modify and extend game logic without needing to recompile the entire project.

## Lua Engine

The integration with Lua is handled by the NLua library, which provides a bridge between C# and Lua. This allows for calling C# functions from Lua and vice-versa.

## Hot-Reloading

One of the key features of the engine is the hot-reloading of scripts. The server monitors files in the `scripts` directory for changes and automatically reloads them when a change is detected. This significantly speeds up the development process, as changes to the game logic can be seen in real-time without restarting the server.

The hot-reloading mechanism is implemented using `FileSystemWatcher` and includes debouncing logic with a `System.Threading.Timer` to prevent multiple reloads from a single file save.

## Entry Point

The main entry point for scripts is the `scripts/main.lua` file. When the server starts, or after a hot-reload, this file is executed first. If a `MainMap` is specified in your project settings (`project.json`), that map will be loaded before `main.lua` is executed. It should contain the main initialization logic for the game world and other important components.

## The `Game` API

To interact with the game world from Lua scripts, a global `Game` object is provided. This object is an instance of the `GameApi` class from C# and provides a set of methods for managing the game state.

### Map Management

#### `Game:CreateMap(width, height, depth)`
Creates a new map with the specified dimensions.

*   `width`: The width of the map (integer).
*   `height`: The height of the map (integer).
*   `depth`: The depth of the map (integer).

**Example:**
```lua
-- Create a 50x50x1 map
Game:CreateMap(50, 50, 1)
```

### Object Management

#### `Game:CreateObject(typeName, x, y, z)`
Creates a new game object of the specified type at the given coordinates.

*   `typeName`: The name of the object type (string).
*   `x`, `y`, `z`: The coordinates to create the object at (integers).

**Example:**
```lua
-- Create a "wall" object at (10, 5, 0)
local wall = Game:CreateObject("wall", 10, 5, 0)
```

#### `Game:MoveObject(objectId, newX, newY, newZ)`
Moves an existing game object to new coordinates.

*   `objectId`: The unique identifier of the object (integer).
*   `newX`, `newY`, `newZ`: The new coordinates for the object (integers).

**Example:**
```lua
-- Move the object with ID `wall.Id` to (11, 5, 0)
Game:MoveObject(wall.Id, 11, 5, 0)
```

#### `Game:DestroyObject(objectId)`
Removes a game object from the world.

*   `objectId`: The unique identifier of the object (integer).

**Example:**
```lua
-- Destroy the object
Game:DestroyObject(wall.Id)
```

### Object Properties

#### `Game:SetObjectProperty(objectId, key, value)`
Sets the value of a property for a game object.

*   `objectId`: The unique identifier of the object (integer).
*   `key`: The name of the property (string).
*   `value`: The new value of the property (can be a string, number, or boolean).

**Example:**
```lua
-- Set the "health" property for an object
Game:SetObjectProperty(player.Id, "health", 100)
```

#### `Game:GetObjectProperty(objectId, key)`
Gets the value of a property from a game object.

*   `objectId`: The unique identifier of the object (integer).
*   `key`: The name of the property (string).

**Example:**
```lua
-- Get the "health" property
local playerHealth = Game:GetObjectProperty(player.Id, "health")
print("Player health: " .. tostring(playerHealth))
```
