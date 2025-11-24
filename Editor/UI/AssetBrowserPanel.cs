using Core;
using ImGuiNET;

namespace Editor.UI
{
    public class AssetBrowserPanel
    {
        private readonly AssetManager _assetManager;

        public AssetBrowserPanel(AssetManager assetManager)
        {
            _assetManager = assetManager;
        }

        public void Draw()
        {
            ImGui.Begin("Assets");
            foreach (var asset in _assetManager.GetAssetPaths())
            {
                ImGui.Text(asset);
            }
            ImGui.End();
        }
    }
}
