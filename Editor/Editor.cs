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

        private readonly EditorState _editorState = new EditorState();
        private readonly SelectionManager _selectionManager = new SelectionManager();
        private readonly AssetBrowser _assetBrowser = new AssetBrowser();
        private GameState _gameState = new GameState(); // This would be loaded from a file in a real editor.

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

            window.Run();
        }

        private void OnLoad()
        {
            if (window != null)
            {
                gl = window.CreateOpenGL();
                inputContext = window.CreateInput();
                imGuiController = new ImGuiController(gl, window, inputContext);
            }

            // In a real editor, you would load a map here.
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

            // In a real editor, you would have ImGui rendering here.
            ImGui.DockSpaceOverViewport(ImGui.GetMainViewport());

            DrawMenuBar();
            DrawWindows();

            imGuiController?.Render();
        }

        private void DrawMenuBar()
        {
            // Placeholder for a menu bar UI.
        }

        private void DrawWindows()
        {
            ImGui.Begin("Main");
            if (ImGui.BeginTabBar("MainTabBar"))
            {
                if (ImGui.BeginTabItem("Map"))
                {
                    DrawMap();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Assets"))
                {
                    DrawAssetWindow();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
        }

        private void DrawAssetWindow()
        {
            // Placeholder for an asset browser window UI.
            if (_gameState.Map != null)
            {
                var assets = _assetBrowser.GetAssets(_gameState.Map);
                ImGui.Text("Asset Browser");
                foreach (var asset in assets)
                {
                    ImGui.Text(asset);
                }
            }
        }

        private void DrawMap()
        {
            // Placeholder for rendering the map and its contents.
            ImGui.Text("Map View");
        }

        private void HandleMouseInput()
        {
            // Placeholder for handling mouse input for selection, eyedropper, etc.
        }

        private void OnClose()
        {
            imGuiController?.Dispose();
            gl?.Dispose();
        }
    }
}
