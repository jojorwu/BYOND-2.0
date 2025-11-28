using Core;
using ImGuiNET;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Editor.UI
{
    public class AssetBrowserPanel
    {
        private readonly Project _project;
        private readonly EditorContext _editorContext;
        private readonly HashSet<string> _ignoredDirectories = new() { ".git", "bin", "obj" };

        public AssetBrowserPanel(Project project, EditorContext editorContext)
        {
            _project = project;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Assets");

            if (ImGui.BeginPopupContextWindow("AssetBrowserContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.MenuItem("New Lua Script")) CreateNewScript(_project.RootPath, ".lua");
                if (ImGui.MenuItem("New DM Script")) CreateNewScript(_project.RootPath, ".dm");
                ImGui.EndPopup();
            }

            DrawDirectoryNode(_project.RootPath);
            ImGui.End();
        }

        private void DrawDirectoryNode(string path)
        {
            var directoryInfo = new DirectoryInfo(path);

            foreach (var directory in directoryInfo.GetDirectories().Where(d => !_ignoredDirectories.Contains(d.Name) && !d.Attributes.HasFlag(FileAttributes.Hidden)))
            {
                bool isTreeNodeOpen = ImGui.TreeNodeEx(directory.Name, ImGuiTreeNodeFlags.OpenOnArrow);

                if (ImGui.BeginPopupContextItem($"ContextMenu_{directory.FullName}"))
                {
                    if (ImGui.MenuItem("New Lua Script")) CreateNewScript(directory.FullName, ".lua");
                    if (ImGui.MenuItem("New DM Script")) CreateNewScript(directory.FullName, ".dm");
                    ImGui.EndPopup();
                }

                if (isTreeNodeOpen)
                {
                    DrawDirectoryNode(directory.FullName);
                    ImGui.TreePop();
                }
            }

            foreach (var file in directoryInfo.GetFiles().Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden)))
            {
                if (ImGui.Selectable(file.Name, false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        _editorContext.OpenFile(file.FullName);
                    }
                }
            }
        }

        private void CreateNewScript(string directoryPath, string extension)
        {
            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            string baseName = "new_script";
            string fileName = $"{baseName}{extension}";
            int i = 1;
            while (File.Exists(Path.Combine(directoryPath, fileName)))
            {
                fileName = $"{baseName}_{i++}{extension}";
            }
            File.WriteAllText(Path.Combine(directoryPath, fileName), "");
        }
    }
}
