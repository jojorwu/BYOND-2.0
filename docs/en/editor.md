# Editor Guide

The BYOND 2.0 Editor is a graphical application built with Silk.NET and ImGui that allows developers to create and modify game maps (`.dmm` files) and define object properties.

*(Note: This guide will be updated with screenshots in the future to better illustrate the interface.)*

## How to Run the Editor

The easiest way to run the editor is by using the provided shell script from the project root:

```bash
./run_editor.sh
```

## User Interface Overview

The editor interface is composed of several key panels:

*   **Map View:** The central panel that provides a visual, tile-based representation of your game map. You can navigate the map, select tiles, and place objects here.
*   **Object Tree:** Located on the left, this panel displays a hierarchical tree of all available object types defined in your DM code (e.g., `/obj`, `/turf`, `/mob`). This is used to select objects for placement.
*   **Properties Panel:** Located on the right, this panel displays the properties of the currently selected object or tile on the map. You can view and edit properties such as an object's variables or a turf's type.
*   **Menu Bar:** At the top of the window, the menu bar provides access to file operations (New, Open, Save) and other editor functions.

## Basic Workflow

### Creating a New Map

1.  Go to `File > New Map` in the menu bar.
2.  A dialog will appear asking for the map's dimensions (width and height in tiles).
3.  Enter the desired dimensions and click "Create". A new, empty map will be created in the Map View.

### Placing Objects

1.  **Select an Object Type:** In the **Object Tree** panel, navigate the hierarchy and click on the object type you wish to place (e.g., `/obj/item/apple`).
2.  **Select a Tile:** In the **Map View**, left-click on the tile where you want to place the object.
3.  An instance of the selected object will be placed on that tile. You can place multiple objects by repeatedly clicking on different tiles.

### Editing Properties

1.  **Select an Instance:** In the **Map View**, right-click on a tile that contains the object you want to inspect.
2.  **View Properties:** The **Properties Panel** on the right will update to show all the variables and their current values for the selected object instance.
3.  **Modify Properties:** You can click on the values in the Properties Panel to edit them directly. For example, you can change an apple's `bites_left` variable before the game even starts.

### Saving the Map

1.  Go to `File > Save Map As...` in the menu bar.
2.  A file dialog will open. Navigate to the `maps` directory, enter a name for your map (e.g., `main.dmm`), and click "Save".

Your map is now saved and can be loaded by the server at runtime.
