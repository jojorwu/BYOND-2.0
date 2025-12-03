# Client Guide

The BYOND 2.0 Client is a console application responsible for rendering the game world, handling user input, and communicating with the game server.

## Key Responsibilities

*   **Rendering:** The client renders the game state received from the server.
*   **User Input:** It captures keyboard and mouse input and sends it to the server.
*   **Networking:** The client establishes and maintains a connection with the server, receiving game state updates and sending user commands.

## How to Run the Client

To run the game client, you first need to ensure the server is running. Then, execute the following command from the root of the project:

```bash
dotnet run --project Client/Client.csproj
```

Upon launch, the client will attempt to connect to the server specified in its configuration.
