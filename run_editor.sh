#!/bin/bash
# This script builds and runs the Editor project.

# Set up the .NET environment
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools

# Ensure the project is built
dotnet build Editor/Editor.csproj

# Run the editor
dotnet run --project Editor/Editor.csproj
