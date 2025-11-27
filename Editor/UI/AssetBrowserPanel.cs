using Core;
using ImGuiNET;
using System.IO;

namespace Editor.UI
{
    public class AssetBrowserPanel
    {
        private readonly AssetManager _assetManager;
        private readonly Project _project;

        public AssetBrowserPanel(AssetManager assetManager, Project project)
        {
            _assetManager = assetManager;
            _project = project;
        }

        public void Draw()
        {
            ImGui.Begin("Assets");

            if (ImGui.BeginPopupContextWindow("AssetBrowserContextMenu"))
            {
                if (ImGui.MenuItem("New Lua Script"))
                {
                    CreateNewScript(".lua");
                }
                if (ImGui.MenuItem("New DM Script"))
                {
                    CreateNewScript(".dm");
                }
                ImGui.EndPopup();
            }

            foreach (var asset in _assetManager.GetAssetPaths())
            {
                ImGui.Text(asset);
            }
            ImGui.End();
        }

        private void CreateNewScript(string extension)
        {
            var scriptsPath = _project.GetFullPath(Constants.ScriptsRoot);
            if (!Directory.Exists(scriptsPath))
            {
                Directory.CreateDirectory(scriptsPath);
            }

            string baseName = "new_script";
            string fileName = $"{baseName}{extension}";
            int i = 1;
            while (File.Exists(Path.Combine(scriptsPath, fileName)))
            {
                fileName = $"{baseName}_{i++}{extension}";
            }

            File.WriteAllText(Path.Combine(scriptsPath, fileName), "");
        }
    }
}
