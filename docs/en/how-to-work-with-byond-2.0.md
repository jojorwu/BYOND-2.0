# How to work with BYOND 2.0

This guide will help you set up your development environment, build the project, and run the BYOND 2.0 server.

## 1. Environment Setup

To work with the project, you will need the .NET 8.0 SDK.

### Installing the .NET 8.0 SDK

You can install the .NET 8.0 SDK by running the following commands in the root folder of the project:

```bash
chmod +x ./dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

After installation, you need to add .NET to the `PATH` for the current session:

```bash
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools
```

## 2. Building the Project

After installing the .NET SDK, you can build the project. It is recommended to build the entire solution, but you can also build individual projects. To build the entire solution, run the following command:

```bash
dotnet build BYOND2.0.slnx
```

## 3. Running the Server

To run the game server, execute the following command:

```bash
dotnet run --project Server/Server.csproj
```

After launching, the server will start monitoring the `scripts` directory for changes and will automatically reload them as needed.
