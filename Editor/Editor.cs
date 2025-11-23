using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;

namespace Editor
{
    /// <summary>
    /// The main editor application class.
    /// </summary>
    public class Editor
    {
        private IWindow? window;
        private GL? gl;
        private IInputContext? inputContext;
        private ImGuiController? imGuiController;
        private TextureManager? _textureManager;
        private SpriteRenderer? _spriteRenderer;

        private readonly ToolManager _toolManager = new ToolManager();
        private readonly EditorState _editorState = new EditorState();
        private readonly SelectionManager _selectionManager = new SelectionManager();
        private readonly AssetManager _assetManager = new AssetManager();
        private readonly AssetBrowser _assetBrowser;
        private readonly ScriptManager _scriptManager = new ScriptManager();
        private readonly ObjectTypeManager _objectTypeManager = new ObjectTypeManager();
        private readonly MapLoader _mapLoader;
        private GameState _gameState = new GameState();

        public ObjectType? SelectedObjectType { get; set; }
        public int CurrentZLevel { get; private set; } = 0;

        // Script Editor State
        private string[] _scriptFiles = Array.Empty<string>();
        private string? _selectedScript;
        private string _scriptContent = string.Empty;

        public Editor()
        {
            _assetBrowser = new AssetBrowser(_assetManager);
            _mapLoader = new MapLoader(_objectTypeManager);
        }

        /// <summary>
        /// Runs the editor application.
        /// </summary>
        public void Run()
        {
            var options = WindowOptions.Default;
            options.Title = "BYOND 2.0 Editor";
            options.Size = new Vector2D<int>(1280, 720);

            window = Window.Create(options);

            window.Load += OnLoad;
            window.Render += OnRender;
            window.Closing += OnClose;
            window.FileDrop += OnFileDrop;

            window.Run();
        }

        private void OnFileDrop(string[] paths)
        {
            foreach (var path in paths)
            {
                _assetManager.ImportAsset(path);
            }
        }

        private void OnLoad()
        {
            if (window != null)
            {
                gl = window.CreateOpenGL();
                inputContext = window.CreateInput();
                imGuiController = new ImGuiController(gl, window, inputContext);
                _textureManager = new TextureManager(gl);
                _spriteRenderer = new SpriteRenderer(gl);

                _toolManager.SetActiveTool(_toolManager.Tools.FirstOrDefault(), this);
                _scriptFiles = _scriptManager.GetScriptFiles();
            }

            // Create some dummy object types for now
            var wall = new ObjectType("wall");
            wall.DefaultProperties["SpritePath"] = "assets/wall.png";
            _objectTypeManager.RegisterObjectType(wall);

            var floor = new ObjectType("floor");
            floor.DefaultProperties["SpritePath"] = "assets/floor.png";
            _objectTypeManager.RegisterObjectType(floor);


            _gameState.Map = new Map(10, 10, 1);
        }

        private void OnRender(double deltaTime)
        {
            imGuiController?.Update((float)deltaTime);

            HandleMouseInput();
            Draw();
        }

        private void Draw()
        {
            if (gl != null)
            {
                gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
                gl.Clear(ClearBufferMask.ColorBufferBit);
            }

            ImGui.DockSpaceOverViewport(ImGui.GetMainViewport());

            DrawMenuBar();
            DrawWindows();

            imGuiController?.Render();
        }

        private void DrawMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save Map"))
                    {
                        if(_gameState.Map != null)
                            _mapLoader.SaveMap(_gameState.Map, "maps/default.json");
                        Console.WriteLine("Map saved to maps/default.json");
                    }
                    if (ImGui.MenuItem("Load Map"))
                    {
                        var loadedMap = _mapLoader.LoadMap("maps/default.json");
                        if (loadedMap != null)
                        {
                            _gameState.Map = loadedMap;
                            Console.WriteLine("Map loaded from maps/default.json");
                        }
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }

        private void DrawWindows()
        {
            DrawToolbox();
            DrawPropertiesWindow();
            DrawMapControlsWindow();

            ImGui.Begin("Main");
            if (ImGui.BeginTabBar("MainTabBar"))
            {
                if (ImGui.BeginTabItem("Map"))
                {
                    DrawMap();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Types"))
                {
                    DrawTypesWindow();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Scripts"))
                {
                    DrawScriptEditor();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        private void DrawToolbox()
        {
            ImGui.Begin("Tools");
            foreach (var tool in _toolManager.Tools)
            {
                if (ImGui.Button(tool.Name))
                {
                    _toolManager.SetActiveTool(tool, this);
                }
            }
            ImGui.End();
        }

        private void DrawTypesWindow()
        {
            ImGui.BeginChild("Object Types");
            foreach (var objectType in _objectTypeManager.GetAllObjectTypes())
            {
                if (ImGui.Selectable(objectType.Name, SelectedObjectType == objectType))
                {
                    SelectedObjectType = objectType;
                    Console.WriteLine($"Selected object type: {objectType.Name}");
                }
            }
            ImGui.EndChild();
        }

        private void DrawMap()
        {
            ImGui.BeginChild("Map View");

            var windowSize = ImGui.GetWindowSize();
            var projection = Matrix4X4.CreateOrthographicOffCenter(0.0f, windowSize.X, windowSize.Y, 0.0f, -1.0f, 1.0f);

            if (_gameState.Map != null && _spriteRenderer != null && _textureManager != null && CurrentZLevel < _gameState.Map.Depth)
            {
                for (int y = 0; y < _gameState.Map.Height; y++)
                {
                    for (int x = 0; x < _gameState.Map.Width; x++)
                    {
                        var turf = _gameState.Map.GetTurf(x, y, CurrentZLevel);
                        if (turf != null)
                        {
                            foreach (var gameObject in turf.Contents)
                            {
                                var spritePath = gameObject.GetProperty<string>("SpritePath");
                                if (!string.IsNullOrEmpty(spritePath))
                                {
                                    uint textureId = _textureManager.GetTexture(spritePath);
                                    if (textureId != 0)
                                    {
                                        _spriteRenderer.Draw(textureId, new Vector2D<int>(x * Constants.TileSize, y * Constants.TileSize), new Vector2D<int>(Constants.TileSize, Constants.TileSize), 0.0f, projection);
                                    }
                                }
                            }
                        }
                    }
                }
            }


            if (ImGui.IsWindowHovered())
            {
                var mousePos = ImGui.GetMousePos();
                var windowPos = ImGui.GetWindowPos();
                var localMousePos = new Vector2D<int>((int)(mousePos.X - windowPos.X), (int)(mousePos.Y - windowPos.Y));

                _toolManager.OnMouseMove(this, _gameState, _selectionManager, localMousePos);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    _toolManager.OnMouseDown(this, _gameState, _selectionManager, localMousePos);
                }
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _toolManager.OnMouseUp(this, _gameState, _selectionManager, localMousePos);
                }
            }

            _toolManager.Draw(this, _gameState, _selectionManager);
            ImGui.EndChild();
        }

        private void DrawMapControlsWindow()
        {
            ImGui.Begin("Map Controls");
            ImGui.Text($"Current Z-Level: {CurrentZLevel}");
            if (ImGui.Button("Up"))
            {
                CurrentZLevel++;
            }
            ImGui.SameLine();
            if (ImGui.Button("Down") && CurrentZLevel > 0)
            {
                CurrentZLevel--;
            }
            ImGui.End();
        }

        private void DrawPropertiesWindow()
        {
            ImGui.Begin("Properties");
            var selectedObject = _selectionManager.SelectedObject;
            if (selectedObject != null)
            {
                ImGui.LabelText("ID", selectedObject.Id.ToString());
                ImGui.LabelText("Type", selectedObject.ObjectType.Name);

                int[] position = { selectedObject.X, selectedObject.Y, selectedObject.Z };
                if (ImGui.InputInt3("Position", ref position[0]))
                {
                    selectedObject.SetPosition(position[0], position[1], position[2]);
                }

                // Display all properties (instance and default)
                var allProperties = new Dictionary<string, object>(selectedObject.ObjectType.DefaultProperties);
                foreach (var prop in selectedObject.Properties)
                {
                    allProperties[prop.Key] = prop.Value;
                }

                foreach (var prop in allProperties)
                {
                    string valueStr = prop.Value.ToString() ?? "";
                    if (ImGui.InputText(prop.Key, ref valueStr, 256))
                    {
                        // For simplicity, we are only handling string properties for now.
                        selectedObject.Properties[prop.Key] = valueStr;
                    }
                }
            }
            else
            {
                ImGui.Text("No object selected.");
            }
            ImGui.End();
        }

        private void DrawScriptEditor()
        {
            ImGui.Columns(2, "ScriptEditorColumns", true);

            // Left pane: File browser
            ImGui.BeginChild("ScriptFiles");
            foreach (var scriptFile in _scriptFiles)
            {
                if (ImGui.Selectable(scriptFile, _selectedScript == scriptFile))
                {
                    _selectedScript = scriptFile;
                    _scriptContent = _scriptManager.ReadScriptContent(scriptFile);
                }
            }
            ImGui.EndChild();

            ImGui.NextColumn();

            // Right pane: Text editor
            ImGui.BeginChild("ScriptContent");

            if (ImGui.Button("Save") && _selectedScript != null)
            {
                _scriptManager.WriteScriptContent(_selectedScript, _scriptContent);
            }
            ImGui.SameLine();
            ImGui.Text(_selectedScript ?? "No script selected");

            ImGui.InputTextMultiline("##ScriptContent", ref _scriptContent, 100000, ImGui.GetContentRegionAvail());
            ImGui.EndChild();

            ImGui.Columns(1);
        }

        private void HandleMouseInput()
        {
            // Input is now handled within the DrawMap method to ensure correct window scoping.
        }

        private void OnClose()
        {
            _spriteRenderer?.Dispose();
            _textureManager?.Dispose();
            imGuiController?.Dispose();
            gl?.Dispose();
        }
    }
}
