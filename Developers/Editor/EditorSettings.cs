using System.Numerics;

namespace Editor
{
    public class EditorSettings
    {
        public string ServerExecutablePath { get; set; } = "Server.exe";
        public string ClientExecutablePath { get; set; } = "Client.exe";

        public bool ShowGrid { get; set; } = true;
        public int GridSize { get; set; } = 32;
        public Vector4 GridColor { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
        public bool SnapToGrid { get; set; } = true;

        public bool UseDarkTheme { get; set; } = true;
        public int FontSize { get; set; } = 14;
        public bool AutoSave { get; set; } = false;
        public int AutoSaveIntervalMinutes { get; set; } = 5;

        // Rendering Settings
        public string GraphicsBackend { get; set; } = "OpenGL";
        public bool EnableSsao { get; set; } = true;
        public bool EnableBloom { get; set; } = true;
        public int ResolutionX { get; set; } = 1280;
        public int ResolutionY { get; set; } = 720;
        public bool VSync { get; set; } = true;

        // API Settings
        public string TargetApi { get; set; } = "Lua";
    }
}
