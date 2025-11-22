# Scripting

The BYOND 2.0 game engine uses Lua as its scripting language. This allows developers to quickly modify and extend the game logic without needing to recompile the entire project.

## Lua Engine

Integration with Lua is achieved using the NLua library, which provides a bridge between C# and Lua. This allows for calling C# functions from Lua and vice versa.

## Hot-Reloading

One of the key features of the engine is the hot-reloading of scripts. The server monitors files in the `scripts` directory for changes and automatically reloads them when changes are detected. This significantly speeds up the development process, as changes to the game logic can be seen in real-time without restarting the server.

The hot-reloading mechanism is implemented using `FileSystemWatcher` and includes debouncing with a `System.Threading.Timer` to prevent multiple reloads from occurring in rapid succession.

## Entry Point

The main entry point for scripts is the `scripts/main.lua` file. When the server starts or after a hot-reload, this file is executed first. It should contain the main logic for initializing the game world and other important components.
