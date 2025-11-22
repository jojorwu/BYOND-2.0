# Architecture

The BYOND 2.0 project is a game engine with a client-server architecture, built on .NET 8.0 using C# and Lua for scripting.

## Project Structure

The project is divided into several key components:

*   **Core:** A class library containing the main logic and common components used by both the server and the client. This includes game logic, state management, and the scripting engine.
*   **Server:** A console application that runs the game server. It is responsible for managing the game world, handling client connections, and executing Lua scripts.
*   **Client:** A console application that is the game client. It is responsible for rendering the game world, handling user input, and interacting with the server.
*   **scripts:** A directory containing the Lua scripts that define the game logic.
*   **tests:** A project with unit tests to verify the correct operation of the project components.

## Architecture Diagram

```mermaid
graph TD
    subgraph Client
        C[Client]
    end

    subgraph Server
        S[Server]
        subgraph Scripting
            L[Lua Scripts]
        end
    end

    subgraph Shared Components
        Co[Core]
    end

    C -- "TCP/IP" --> S
    S -- "Uses" --> Co
    C -- "Uses" --> Co
    S -- "Executes" --> L
```
