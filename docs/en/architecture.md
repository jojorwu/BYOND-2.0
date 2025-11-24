# Architecture

The BYOND 2.0 project is a game engine with a client-server architecture, built on .NET 8.0 using C# and Lua for scripting.

## Project Structure

The project is divided into several key components:

*   **`Core`**: A class library containing the core logic and common components used by all other parts of the engine.
*   **`Server`**: A console application that runs the game server. It is responsible for managing the game world and handling connections.
*   **`Editor`**: A graphical application for creating and editing game maps, objects, and scripts.
*   **`Client`**: The game client (under development).
*   **`scripts`**: A directory containing the Lua scripts that define the game's logic.
*   **`tests`**: A project with unit tests.

## Core Concepts

### Game State (`GameState`)
A central class that holds the entire current state of the game world, including the map (`Map`) and a list of all game objects (`GameObjects`).

### Object Model (`GameObject` and `ObjectType`)
The engine uses an inheritance-based system for defining object types.
*   **`ObjectType`**: Defines a template for objects, including their name, parent type, and default properties.
*   **`GameObject`**: Represents an instance of an object in the game world, with its own unique `Id`, coordinates, and instance properties that can override the `ObjectType`'s properties.

### Scripting API (`GameApi`)
The C# `GameApi` class serves as a bridge between C# and Lua. An instance of this class is provided to Lua as the global `Game` object, allowing scripts to interact with the game world safely.

## Architecture Diagram

```mermaid
graph TD
    subgraph "User Tools"
        Editor[<i class='fa fa-pencil-ruler'></i> Editor]
    end

    subgraph "Game Applications"
        Client[<i class='fa fa-gamepad'></i> Client]
        Server[<i class='fa fa-server'></i> Server]
    end

    subgraph "Engine Core (Core)"
        CoreLib[Core.dll]
        GameApi[GameApi]
        GameState[GameState]
        ObjectTypeManager[ObjectTypeManager]
        MapLoader[MapLoader]
    end

    subgraph "Scripts & Data"
        LuaScripts[<i class='fa fa-file-code'></i> Lua Scripts]
        ProjectFiles[<i class='fa fa-folder-open'></i> Project Files<br>(maps, types)]
    end

    Editor -- "Uses" --> CoreLib
    Client -- "Uses" --> CoreLib
    Server -- "Uses" --> CoreLib

    CoreLib --> GameApi
    CoreLib --> GameState
    CoreLib --> ObjectTypeManager
    CoreLib --> MapLoader

    Server -- "Executes" --> LuaScripts
    LuaScripts -- "Calls" --> GameApi

    GameApi -- "Mutates" --> GameState
    GameApi -- "Uses" --> ObjectTypeManager
    GameApi -- "Uses" --> MapLoader

    Editor -- "Loads/Saves" --> ProjectFiles
    MapLoader -- "Reads/Writes" --> ProjectFiles
    ObjectTypeManager -- "Reads/Writes" --> ProjectFiles

    style Editor fill:#D8BFD8,stroke:#333,stroke-width:2px
    style Client fill:#ADD8E6,stroke:#333,stroke-width:2px
    style Server fill:#90EE90,stroke:#333,stroke-width:2px
```
