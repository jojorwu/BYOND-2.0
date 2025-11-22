# Project Architecture

This document describes the architecture of the BYOND-2.0 game engine.

## Overview

The project consists of two main components:

1.  **Core**: A class library containing the core engine logic.
2.  **Server**: A console application that hosts the engine and manages the game loop.

## Components

### Core

The `Core` component is responsible for handling scripts. It uses the [NLua](https://github.com/NLua/NLua) library to embed the Lua interpreter into the .NET environment.

-   **`Scripting.cs`**: This class provides a wrapper for NLua's functionality, allowing the engine to execute Lua strings or script files.

### Server

The `Server` component is the application's entry point. Its responsibilities include:

-   Initializing the `Core` component.
-   Loading and executing the initial Lua script (`scripts/main.lua`).
-   Monitoring the `scripts` directory for changes to Lua files. When a change is detected, the server automatically reloads the modified script, enabling hot-reloading.

## Data Flow

1.  The `Server` application starts.
2.  It creates an instance of the `Core.Scripting` class.
3.  The server executes the `scripts/main.lua` file to start the game.
4.  The server watches for changes to `.lua` files in the `scripts` directory.
5.  When a file is modified, the server uses the `Core.Scripting` instance to re-execute the updated script.
