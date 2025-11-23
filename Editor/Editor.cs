using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Core;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Input;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Editor
{
    public class Editor
    {
        private enum AppState
        {
            MainMenu,
            Editing,
            Settings
        }

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
        private readonly GameApi _gameApi;
        private EngineSettings _engineSettings;
        private GameState _gameState = new GameState();

        public ObjectType? SelectedObjectType { get; set; }
        public int CurrentZLevel { get; private set; } = 0;

        private AppState _appState = AppState.MainMenu;
        private string _newTypeName = string.Empty;
        private string _objectBrowserFilter = string.Empty;
        private string[] _scriptFiles = Array.Empty<string>();
        private string? _selectedScript;
        private string _scriptContent = string.Empty;

        // Add Property Dialog State
        private bool _showAddPropertyDialog = false;
        private string _newPropertyName = string.Empty;
        private int _newPropertyTypeIndex = 0;
        private readonly string[] _propertyTypes = { "String", "Integer", "Float", "Boolean" };

        // New Map Dialog State
        private bool _showNewMapDialog = false;
        private int _newMapWidth = 20;
        private int _newMapHeight = 20;
        private int _newMapDepth = 1;

        public Editor()
        {
            _assetBrowser = new AssetBrowser(_assetManager);
            _engineSettings = EngineSettings.Load();
            _objectTypeManager.LoadTypes();
            _mapLoader = new MapLoader(_objectTypeManager);
            _gameApi = new GameApi(_gameState, _objectTypeManager);
        }

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
            foreach (var path in paths) _assetManager.ImportAsset(path);
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
            ApplyGodotTheme();
        }

        private void OnRender(double deltaTime)
        {
            imGuiController?.Update((float)deltaTime);
            Draw();
        }

        private void Draw()
        {
            gl?.ClearColor(0.12f, 0.12f, 0.12f, 1.00f);
            gl?.Clear(ClearBufferMask.ColorBufferBit);

            switch (_appState)
            {
                case AppState.MainMenu: DrawMainMenu(); break;
                case AppState.Editing: DrawEditor(); break;
                case AppState.Settings: DrawSettingsScreen(); break;
            }

            imGuiController?.Render();
        }

        private void DrawEditor()
        {
            ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);
            DrawMenuBar();
            DrawObjectBrowser();
            DrawInspector();
            DrawAssetBrowser();
            DrawToolbox();

            if (_showAddPropertyDialog)
            {
                DrawAddPropertyDialog();
            }

            ImGui.Begin("Viewport");
            HandleMouseInput();
            DrawMapContent();
            ImGui.End();

            DrawScriptEditor();
        }

        private void DrawMainMenu()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.Begin("Main Menu", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);

            ImGui.Text("BYOND 2.0");
            ImGui.Separator();

            if (ImGui.Button("New Map", new Vector2(120, 0)))
            {
                _showNewMapDialog = true;
            }

            if (_showNewMapDialog)
            {
                DrawNewMapDialog();
            }

            if (ImGui.Button("Load Map", new Vector2(120, 0)))
            {
                _gameApi.LoadMap("maps/default.json");
                _appState = AppState.Editing;
            }
            if (ImGui.Button("Settings", new Vector2(120, 0))) _appState = AppState.Settings;
            if (ImGui.Button("Exit", new Vector2(120, 0))) window?.Close();
            ImGui.End();

            if (_showAddPropertyDialog)
            {
                DrawAddPropertyDialog();
            }
        }

        private void DrawAddPropertyDialog()
        {
            if (SelectedObjectType == null)
            {
                _showAddPropertyDialog = false;
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(300, 120));
            ImGui.Begin("Add New Property", ref _showAddPropertyDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            ImGui.InputText("Property Name", ref _newPropertyName, 64);
            ImGui.Combo("Property Type", ref _newPropertyTypeIndex, _propertyTypes, _propertyTypes.Length);
            ImGui.Spacing();
            if (ImGui.Button("Add", new Vector2(120, 0)))
            {
                if (!string.IsNullOrWhiteSpace(_newPropertyName) && !SelectedObjectType.DefaultProperties.ContainsKey(_newPropertyName))
                {
                    object defaultValue = _propertyTypes[_newPropertyTypeIndex] switch
                    {
                        "Integer" => 0,
                        "Float" => 0.0f,
                        "Boolean" => false,
                        _ => ""
                    };
                    SelectedObjectType.DefaultProperties.Add(_newPropertyName, defaultValue);
                    _objectTypeManager.SaveTypes();
                    _showAddPropertyDialog = false;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showAddPropertyDialog = false;
            }
            ImGui.End();
        }

        private void DrawNewMapDialog()
        {
            ImGui.SetNextWindowSize(new Vector2(300, 150));
            ImGui.Begin("Create New Map", ref _showNewMapDialog, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
            ImGui.InputInt("Width", ref _newMapWidth);
            ImGui.InputInt("Height", ref _newMapHeight);
            ImGui.InputInt("Depth", ref _newMapDepth);
            _newMapWidth = Math.Max(1, _newMapWidth);
            _newMapHeight = Math.Max(1, _newMapHeight);
            _newMapDepth = Math.Max(1, _newMapDepth);

            ImGui.Spacing();
            if (ImGui.Button("Create", new Vector2(120, 0)))
            {
                _gameApi.CreateMap(_newMapWidth, _newMapHeight, _newMapDepth);
                _appState = AppState.Editing;
                _showNewMapDialog = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _showNewMapDialog = false;
            }
            ImGui.End();
        }

        private void DrawMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Save Map")) _gameApi.SaveMap("maps/default.json");
                    if (ImGui.MenuItem("Load Map")) _gameApi.LoadMap("maps/default.json");
                    if (ImGui.MenuItem("Back to Main Menu")) _appState = AppState.MainMenu;
                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("Edit"))
                {
                    if (ImGui.MenuItem("Engine Settings")) _appState = AppState.Settings;
                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }
        }

        private void DrawSettingsScreen()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            ImGui.Begin("Engine Settings", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.Text("Multi-threading Settings");
            ImGui.Separator();
            bool enableMultiThreading = _engineSettings.EnableMultiThreading;
            if (ImGui.Checkbox("Enable Multi-threading", ref enableMultiThreading)) _engineSettings.EnableMultiThreading = enableMultiThreading;
            if (enableMultiThreading)
            {
                bool isAuto = _engineSettings.NumberOfThreads == 0;
                if (ImGui.RadioButton("Automatic", isAuto)) _engineSettings.NumberOfThreads = 0;
                if (ImGui.RadioButton("Manual", !isAuto))
                {
                    if (isAuto) _engineSettings.NumberOfThreads = Environment.ProcessorCount;
                }
                if (!isAuto)
                {
                    int numThreads = _engineSettings.NumberOfThreads;
                    if (ImGui.InputInt("Number of Threads", ref numThreads, 1, 1)) _engineSettings.NumberOfThreads = Math.Max(1, numThreads);
                }
                ImGui.Text($"Effective number of threads: {_engineSettings.EffectiveNumberOfThreads}");
            }
            ImGui.Separator();
            if (ImGui.Button("Save Settings", new Vector2(120, 0))) _engineSettings.Save();
            if (ImGui.Button("Back", new Vector2(120, 0))) _appState = AppState.MainMenu;
            ImGui.End();
        }

        private void DrawToolbox()
        {
            ImGui.Begin("Tools");
            foreach (var tool in _toolManager.Tools)
            {
                if (ImGui.Button(tool.Name)) _toolManager.SetActiveTool(tool, this);
            }
            ImGui.End();
        }

        private void DrawObjectBrowser()
        {
            ImGui.Begin("Object Tree");
            ImGui.InputText("Search", ref _objectBrowserFilter, 64);
            ImGui.Separator();
            var filteredTypes = _objectTypeManager.GetAllObjectTypes()
                .Where(t => t.Name.Contains(_objectBrowserFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var objectType in filteredTypes)
            {
                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (SelectedObjectType == objectType) flags |= ImGuiTreeNodeFlags.Selected;
                string icon = "[O] ";
                if (objectType.Name.Contains("mob")) icon = "[M] ";
                if (objectType.Name.Contains("turf")) icon = "[T] ";
                if (ImGui.TreeNodeEx(icon + objectType.Name, flags))
                {
                    if (ImGui.IsItemClicked()) SelectedObjectType = objectType;
                    ImGui.TreePop();
                }
            }
            ImGui.End();
        }

        private void DrawInspector()
        {
            ImGui.Begin("Inspector");
            var selectedObject = _selectionManager.SelectedObject;
            if (selectedObject != null)
            {
                ImGui.Text($"ID: {selectedObject.Id}");
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), selectedObject.ObjectType.Name);
                ImGui.Separator();
                if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    int[] pos = { selectedObject.X, selectedObject.Y, selectedObject.Z };
                    if (ImGui.DragInt3("Position", ref pos[0], 0.1f)) selectedObject.SetPosition(pos[0], pos[1], pos[2]);
                }
                if (ImGui.CollapsingHeader("Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var allProps = new Dictionary<string, object>(selectedObject.ObjectType.DefaultProperties);
                    foreach (var prop in selectedObject.Properties) allProps[prop.Key] = prop.Value;
                    foreach (var prop in allProps)
                    {
                        string key = prop.Key;
                        object val = prop.Value;
                        if (val is int iVal) { if (ImGui.DragInt(key, ref iVal)) selectedObject.Properties[key] = iVal; }
                        else if (val is float fVal) { if (ImGui.DragFloat(key, ref fVal)) selectedObject.Properties[key] = fVal; }
                        else if (val is bool bVal) { if (ImGui.Checkbox(key, ref bVal)) selectedObject.Properties[key] = bVal; }
                        else
                        {
                            string sVal = val?.ToString() ?? "";
                            if (ImGui.InputText(key, ref sVal, 256)) selectedObject.Properties[key] = sVal;
                        }
                    }
                }
                ImGui.Spacing();
                if (ImGui.Button("Delete Object", new Vector2(-1, 0)))
                {
                    _gameApi.DestroyObject(selectedObject.Id);
                    _selectionManager.Deselect(selectedObject);
                }
            }
            }
            else if (SelectedObjectType != null)
            {
                ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), SelectedObjectType.Name);
                ImGui.Separator();

                if (ImGui.CollapsingHeader("Default Properties", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    var properties = SelectedObjectType.DefaultProperties;
                    var keys = properties.Keys.ToList();
                    foreach (var key in keys)
                    {
                        var val = properties[key];
                        bool changed = false;
                        if (val is int iVal)
                        {
                            if (ImGui.DragInt(key, ref iVal))
                            {
                                properties[key] = iVal;
                                changed = true;
                            }
                        }
                        else if (val is float fVal)
                        {
                            if (ImGui.DragFloat(key, ref fVal))
                            {
                                properties[key] = fVal;
                                changed = true;
                            }
                        }
                        else if (val is bool bVal)
                        {
                            if (ImGui.Checkbox(key, ref bVal))
                            {
                                properties[key] = bVal;
                                changed = true;
                            }
                        }
                        else
                        {
                            string sVal = val?.ToString() ?? "";
                            if (ImGui.InputText(key, ref sVal, 256))
                            {
                                properties[key] = sVal;
                                changed = true;
                            }
                        }
                        if(changed) _objectTypeManager.SaveTypes();
                    }
                }
                ImGui.Spacing();
                if (ImGui.Button("Add Property..."))
                {
                    _showAddPropertyDialog = true;
                    _newPropertyName = string.Empty;
                    _newPropertyTypeIndex = 0;
                }
            }
            else ImGui.TextDisabled("Select an object or type to inspect properties.");
            ImGui.End();
        }

        private void DrawAssetBrowser()
        {
            ImGui.Begin("FileSystem");
            ImGui.Columns(2, "fileSystemCols", true);
            ImGui.SetColumnWidth(0, 150);
            if (ImGui.TreeNodeEx("Assets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TreeNodeEx("Sprites", ImGuiTreeNodeFlags.Leaf);
                ImGui.TreeNodeEx("Scripts", ImGuiTreeNodeFlags.Leaf);
                ImGui.TreeNodeEx("Maps", ImGuiTreeNodeFlags.Leaf);
                ImGui.TreePop();
            }
            ImGui.NextColumn();
            var assets = _assetBrowser.GetAssets();
            float padding = 8.0f;
            float thumbnailSize = 64.0f;
            float cellSize = thumbnailSize + padding;
            float panelWidth = ImGui.GetContentRegionAvail().X;
            int columnCount = (int)(panelWidth / cellSize);
            if (columnCount < 1) columnCount = 1;
            ImGui.BeginTable("AssetsTable", columnCount);
            foreach (var asset in assets)
            {
                ImGui.TableNextColumn();
                ImGui.PushID(asset);
                if (ImGui.Button(Path.GetFileName(asset), new Vector2(thumbnailSize, thumbnailSize))) { }
                ImGui.TextWrapped(Path.GetFileNameWithoutExtension(asset));
                ImGui.PopID();
            }
            ImGui.EndTable();
            ImGui.End();
        }

        private void DrawMapContent()
        {
            var windowSize = ImGui.GetWindowSize();
            var projection = Matrix4X4.CreateOrthographicOffCenter(0.0f, windowSize.X, windowSize.Y, 0.0f, -1.0f, 1.0f);

            if (_gameState.Map != null && _spriteRenderer != null && _textureManager != null && CurrentZLevel < _gameState.Map.Depth)
            {
                for (int y = 0; y < _gameState.Map.Height; y++)
                {
                    for (int x = 0; x < _gameState.Map.Width; x++)
                    {
                        var turf = _gameState.Map.GetTurf(x, y, CurrentZLevel);
                        if (turf == null) continue;
                        foreach (var gameObject in turf.Contents)
                        {
                            var spritePath = gameObject.GetProperty<string>("SpritePath", _objectTypeManager);
                            if (string.IsNullOrEmpty(spritePath)) continue;
                            uint textureId = _textureManager.GetTexture(spritePath);
                            if (textureId != 0)
                            {
                                _spriteRenderer.Draw(textureId, new Vector2D<int>(x * Constants.TileSize, y * Constants.TileSize), new Vector2D<int>(Constants.TileSize, Constants.TileSize), 0.0f, projection);
                            }
                        }
                    }
                }
            }
            _toolManager.Draw(this, _gameApi, _gameState, _selectionManager);
        }

        private void DrawScriptEditor()
        {
            ImGui.Begin("Script Editor");
            ImGui.Columns(2, "ScriptEditorColumns", true);
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
            ImGui.BeginChild("ScriptContent");
            if (ImGui.Button("Save") && _selectedScript != null) _scriptManager.WriteScriptContent(_selectedScript, _scriptContent);
            ImGui.SameLine();
            ImGui.Text(_selectedScript ?? "No script selected");
            ImGui.InputTextMultiline("##ScriptContent", ref _scriptContent, 100000, ImGui.GetContentRegionAvail());
            ImGui.EndChild();
            ImGui.Columns(1);
            ImGui.End();
        }

        private void HandleMouseInput()
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows)) return;
            var mousePos = ImGui.GetMousePos();
            var windowPos = ImGui.GetWindowPos();
            var localMousePos = new Vector2D<int>((int)(mousePos.X - windowPos.X), (int)(mousePos.Y - windowPos.Y));
            _toolManager.OnMouseMove(this, _gameApi, _gameState, _selectionManager, localMousePos);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _toolManager.OnMouseDown(this, _gameApi, _gameState, _selectionManager, localMousePos);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) _toolManager.OnMouseUp(this, _gameApi, _gameState, _selectionManager, localMousePos);
        }

        private void OnClose()
        {
            _spriteRenderer?.Dispose();
            _textureManager?.Dispose();
            imGuiController?.Dispose();
            gl?.Dispose();
        }

        private void ApplyGodotTheme()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;
            style.WindowRounding = 4.0f; style.FrameRounding = 4.0f; style.GrabRounding = 4.0f;
            style.PopupRounding = 4.0f; style.ScrollbarRounding = 4.0f; style.TabRounding = 4.0f;
            colors[(int)ImGuiCol.Text] = new Vector4(0.90f, 0.90f, 0.90f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.10f, 0.10f, 0.95f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.05f, 0.05f, 0.05f, 0.50f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.25f, 0.25f, 0.27f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.30f, 0.30f, 0.32f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.15f, 0.15f, 0.15f, 1.00f);
            colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
            var activeColor = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.50f);
            colors[(int)ImGuiCol.HeaderActive] = activeColor;
            colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            colors[(int)ImGuiCol.ButtonActive] = activeColor;
            colors[(int)ImGuiCol.Tab] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.TabHovered] = new Vector4(0.26f, 0.59f, 0.98f, 0.80f);
            colors[(int)ImGuiCol.TabActive] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.CheckMark] = activeColor;
            colors[(int)ImGuiCol.SliderGrab] = activeColor;
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.20f, 0.50f, 0.90f, 1.00f);
            colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.26f, 0.59f, 0.98f, 0.70f);
        }
    }
}
