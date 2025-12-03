# API Reference

This document provides a detailed reference for the scripting API available in BYOND 2.0. The API is accessed through the `GameApi` object, which is available globally in Lua and passed as an argument to the entry point in C# scripts.

## IGameApi

The root `IGameApi` interface provides access to all sub-APIs.

*   `Map`: Accesses the `IMapApi`.
*   `Objects`: Accesses the `IObjectApi`.
*   `Scripts`: Accesses the `IScriptApi`.
*   `StdLib`: Accesses the `IStandardLibraryApi`.

---

## IMapApi

The `IMapApi` provides functions for interacting with the game map.

*   **`GetMap()`**: Returns the current `Map` object.
*   **`GetTurf(int x, int y, int z)`**: Returns the `Turf` at the specified coordinates.
*   **`SetTurf(int x, int y, int z, int turfId)`**: Sets the turf at the specified coordinates to a new turf ID.
*   **`LoadMapAsync(string filePath)`**: Asynchronously loads a map from a `.dmm` file.
*   **`SetMap(Map map)`**: Replaces the current map with a new `Map` object.
*   **`SaveMapAsync(string filePath)`**: Asynchronously saves the current map to a `.dmm` file.

---

## IObjectApi

The `IObjectApi` provides functions for creating, destroying, and managing game objects.

*   **`CreateObject(string typeName, int x, int y, int z)`**: Creates a new `GameObject` of the specified type at the given coordinates.
*   **`GetObject(int id)`**: Returns the `GameObject` with the specified ID.
*   **`DestroyObject(int id)`**: Destroys the `GameObject` with the specified ID.
*   **`MoveObject(int id, int x, int y, int z)`**: Moves the `GameObject` with the specified ID to the new coordinates.

---

## IScriptApi

The `IScriptApi` provides functions for interacting with the script files.

*   **`ListScriptFiles()`**: Returns a list of all script file names.
*   **`ScriptFileExists(string filename)`**: Checks if a script file exists.
*   **`ReadScriptFile(string filename)`**: Reads the content of a script file.
*   **`WriteScriptFile(string filename, string content)`**: Writes content to a script file.
*   **`DeleteScriptFile(string filename)`**: Deletes a script file.

---

## IStandardLibraryApi

The `IStandardLibraryApi` provides implementations of common DM standard library functions.

*   **`Locate(string typePath, List<GameObject> container)`**: Finds an object of a specific type within a list of game objects.
*   **`Sleep(int milliseconds)`**: Pauses the current script's execution for a specified duration.
*   **`Range(int distance, int centerX, int centerY, int centerZ)`**: Returns a list of `GameObject`s within a certain range of a central point.
*   **`View(int distance, GameObject viewer)`**: Returns a list of `GameObject`s visible to a specific viewer object.
