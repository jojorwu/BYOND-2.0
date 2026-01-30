# BYOND 2.0 Architectural Overview

This document provides a high-level overview of the project's architecture and guidelines for future development.

## Project Structure

- **Engine/**: Core logic shared between server and client.
  - **Shared/**: Data models, interfaces, and common services (like `EngineManager`, `EventBus`).
    - `Interfaces/`: All public engine interfaces.
    - `Models/`: Core data structures (`DreamValue`, `DreamObject`, `GameObject`).
    - `Services/`: Common service implementations.
    - `Enums/`: Shared enumeration types.
    - `Operations/`: Mathematical and bitwise operations.
  - **Core/**: Engine-specific implementations (VM, scripting, spatial grid).
- **Launchers/**: Entry points for developers and players.
  - `Launcher.Player/`: Lightweight launcher for players to connect and play.
  - `Launcher.Developer/`: Comprehensive tool for game development (compiler, editor, local server).
- **Servers/Server/**: The main game server implementation.
- **Players/Client/**: The main game client implementation.
- **Developers/**: Development tools.
  - `Compiler/`: DM bytecode compiler.
  - `Editor/`: Visual world and script editor.

## Key Architectural Principles

1. **Dependency Injection (DI)**:
   - All components should be resolved via a DI container (`Microsoft.Extensions.DependencyInjection`).
   - Use constructor injection for dependencies.
   - Avoid manual instantiation using `new` for services.

2. **Decoupled Communication**:
   - Use the `IEventBus` for cross-system notifications to avoid tight coupling.
   - Systems should subscribe to events they are interested in and publish events when their state changes.

3. **Service-Oriented Design**:
   - Prefer small, focused services with clear interfaces.
   - Core engine components (VM, ScriptHost, Network) are managed by the `ServerApplication` or launcher main loops.

4. **Performance First**:
   - Critical data paths (like `DreamValue` arithmetic or `DreamObject` access) use `AggressiveInlining` and optimized logic (switch expressions).
   - Minimize allocations in the main game loop (e.g., use `Array.Empty` instead of new lists).
   - Use `IComputeService` for hardware-accelerated tasks (CUDA, ROCm, or SIMD).

5. **Cross-Platform Readiness**:
   - All engine logic should remain platform-agnostic.
   - Platform-specific code should be abstracted behind interfaces.

## Maintenance Guidelines

- **Namespaces**: Keep namespaces aligned with the folder structure (e.g., `Shared.Models`).
- **Logging**: Use `ILogger<T>` for all diagnostic output. Avoid `Console.WriteLine` in core services.
- **Testing**:
  - Add unit tests for every new feature or major refactor.
  - Run all tests before submitting changes.
- **Modularity**: When adding new features, consider if they should live in `Core` (engine) or `Developers` (tools).
