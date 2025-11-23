
using ImGuiNET;
using Core;
using System.IO;
using System.Numerics;

namespace Editor.UI
{
    public class AssetBrowserPanel
    {
        private readonly AssetBrowser _assetBrowser;

        public AssetBrowserPanel(AssetBrowser assetBrowser)
        {
            _assetBrowser = assetBrowser;
        }

        public void Draw()
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
            if (columnCount < 1)
            {
                columnCount = 1;
            }

            ImGui.BeginTable("AssetsTable", columnCount);
            foreach (var asset in assets)
            {
                ImGui.TableNextColumn();
                ImGui.PushID(asset);
                if (ImGui.Button(Path.GetFileName(asset), new Vector2(thumbnailSize, thumbnailSize)))
                {
                    // Handle asset selection
                }
                ImGui.TextWrapped(Path.GetFileNameWithoutExtension(asset));
                ImGui.PopID();
            }
            ImGui.EndTable();
            ImGui.End();
        }
    }
}
