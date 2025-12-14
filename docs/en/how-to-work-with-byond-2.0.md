# How to work with BYOND 2.0

This guide will help you set up your development environment, build the project, and run the BYOND 2.0 server.

## 1. Environment Setup

To work with the project, you will need the .NET 8.0 SDK.

### Installing the .NET 8.0 SDK

The repository includes a script to install the correct .NET SDK version. Run the following commands from the project root:

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

After installing the .NET SDK, you can build the project. To build the entire solution, run the following command from the project root:

```bash
dotnet build BYOND2.0.sln
```

## 3. Running the Server

The easiest way to run the server is by using the provided shell script:

```bash
./run_server.sh
```

This script will build the necessary projects and launch the server. After starting, the server will monitor the `scripts` directory and automatically hot-reload any changes you make to the script files.
