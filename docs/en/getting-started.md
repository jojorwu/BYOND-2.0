# Getting Started with BYOND 2.0

This guide will help you get up and running quickly with the BYOND 2.0 game engine.

## 1. Prerequisites

To work with the project, you will need the **.NET 8.0 SDK**.

### Installing the .NET 8.0 SDK

You can install the .NET 8.0 SDK by running the following commands in the root folder of the project:

```bash
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

After installation, you need to add .NET to your `PATH` for the current session:

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
```

## 2. Project Structure

*   `Core/`: A shared library containing the core engine logic.
*   `Server/`: The server application.
*   `Editor/`: A graphical editor for creating game worlds.
*   `scripts/`: A directory for the Lua scripts that define your game's logic.
*   `assets/`: Resources used in the project (graphics, sounds).
*   `project.json`: Your project's settings file.

## 3. Running the Editor

The Editor is the primary tool for creating and editing game worlds.

> **Note:** In the current execution environment, running the graphical editor is not possible due to missing dependencies. These instructions are for users working in a full desktop environment.

To run the editor, execute the following command:

```bash
dotnet run --project Editor/Editor.csproj
```

On the first launch, the editor will prompt you to create a new project. After creating a project, a `Server.exe` file will be created in the root of your project folder.

## 4. Running the Server

To run the game server, simply execute the `Server.exe` in your project folder.

The server will automatically load the map specified by `MainMap` in your `project.json` file, and then execute the scripts from the `scripts` directory.

## 5. What's Next?

*   **Architecture:** Dive into `architecture.md` to get a deeper understanding of how the engine is designed.
*   **Scripting:** Check out `scripting.md` to learn how to use the `Game` API to create your game logic.
