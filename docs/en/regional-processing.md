# Game Loop and Regional Processing

The BYOND 2.0 server is designed to support games of varying scales. At its core is the **Game Loop**, which is responsible for advancing the game state on each tick. The server can operate in two primary modes: a simple **Global Game Loop** and a high-performance **Regional Processing Loop**.

## Global Game Loop (Default)

This is the default mode and the simplest to understand. When `EnableRegionalProcessing` is set to `false` in `server_config.json`, the server uses the `GlobalGameLoopStrategy`.

### How It Works

1.  On every server tick, the `GameLoop` iterates through **every single game object** and **every single script thread** in the entire world.
2.  It executes the logic for each of them sequentially.

*   **Pros:** Simple, predictable, and suitable for small or single-map games where performance is not a major concern.
*   **Cons:** Does not scale well. As the number of objects and players increases, the time required to complete a single tick grows linearly, which can lead to severe performance degradation (lag).

## Regional Processing Loop

For large, open-world games, processing every object on every tick is inefficient. The Regional Processing system is designed to solve this by only processing parts of the world that are relevant to players. This mode is enabled by setting `EnableRegionalProcessing` to `true` in `server_config.json`.

### How It Works

When enabled, the server uses the `RegionalGameLoopStrategy`, which follows a more complex procedure:

1.  **World Division:** The game world is divided into a grid of fixed-size **Regions**.
2.  **Region Activation:** On each tick, the `RegionManager` determines which regions are "active". By default, this is based on player proximity. Any region within a certain range (`ActivationRange`) of a player is marked as active. Scripts can also force-activate regions using the `IRegionApi`.
3.  **Region Merging (Optional):** If `EnableRegionMerging` is `true`, the system will find groups of adjacent active regions and merge them into a single `MergedRegion`. This is an optimization that reduces the overhead of task scheduling, as one large task is more efficient to manage than many small ones.
4.  **Parallel Execution:** The `GameLoop` then processes the active regions.
    *   First, it executes any "global" script threads that are not tied to a specific object or location.
    *   Then, it iterates over each active `Region` (or `MergedRegion`). For each region, it creates a separate task (`Task.Run`) that executes the logic **only for the objects and script threads located within that region**.
    *   This allows the server to process different parts of the map in parallel, leveraging multiple CPU cores.

Inactive regions are effectively "frozen"—their objects and scripts are not processed, saving significant computational resources.

## Configuration

The regional processing system is configured in `server_config.json`:

```json
{
  "EnableRegionalProcessing": true,
  "EnableRegionMerging": false,
  "MinRegionsToMerge": 2,
  "ActivationRange": 1,
  "ZActivationRange": 0
}
```

*   `EnableRegionalProcessing`: Enables or disables the entire system.
*   `EnableRegionMerging`: Enables or disables the merging of adjacent active regions.
*   `MinRegionsToMerge`: The minimum number of adjacent active regions required to form a `MergedRegion`.
*   `ActivationRange`: The radius (in regions) around a player that will be activated. A value of `1` activates a 3x3 area around the player.
*   `ZActivationRange`: The number of Z-levels above and below a player that will be activated.
