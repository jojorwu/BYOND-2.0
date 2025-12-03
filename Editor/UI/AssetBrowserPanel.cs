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
        public string SelectedFile { get; private set; } = "";

        public AssetBrowserPanel(Project project, EditorContext editorContext)
        {
            _project = project;
            _editorContext = editorContext;
        }

        public void Draw()
        {
            ImGui.Begin("Assets");

            if (ImGui.BeginPopupModal("DeleteConfirm"))
            {
                ImGui.Text($"Are you sure you want to delete '{Path.GetFileName(_itemToDelete)}'?");
                if (ImGui.Button("OK"))
                {
                    DeleteItem(_itemToDelete);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopupContextWindow("AssetBrowserContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.MenuItem("New Lua Script")) CreateNewScript(_editorContext.ProjectRoot, ".lua");
                if (ImGui.MenuItem("New DM Script")) CreateNewScript(_editorContext.ProjectRoot, ".dm");
                ImGui.EndPopup();
            }

            DrawDirectoryNode(_editorContext.ProjectRoot);
            ImGui.End();
        }

        private void DrawDirectoryNode(string path)
        {
            var directoryInfo = new DirectoryInfo(path);

            foreach (var directory in directoryInfo.GetDirectories().Where(d => !_ignoredDirectories.Contains(d.Name) && !d.Attributes.HasFlag(FileAttributes.Hidden)))
            {
                var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick;
                bool isNodeOpen = ImGui.TreeNodeEx(directory.Name, flags);

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.MenuItem("Rename")) { /* TODO: Rename logic */ }
                    if (ImGui.MenuItem("Delete")) { ImGui.OpenPopup("DeleteConfirm"); _itemToDelete = directory.FullName; }
                    ImGui.Separator();
                    if (ImGui.MenuItem("New Lua Script")) CreateNewScript(directory.FullName, ".lua");
                    if (ImGui.MenuItem("New DM Script")) CreateNewScript(directory.FullName, ".dm");
                    ImGui.EndPopup();
                }

                if (isNodeOpen)
                {
                    DrawDirectoryNode(directory.FullName);
                    ImGui.TreePop();
                }
            }

            foreach (var file in directoryInfo.GetFiles().Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden)))
            {
                var flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                if (file.FullName == SelectedFile)
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                ImGui.TreeNodeEx(file.Name, flags);

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.MenuItem("Rename")) { /* TODO: Rename logic */ }
                    if (ImGui.MenuItem("Delete")) { ImGui.OpenPopup("DeleteConfirm"); _itemToDelete = file.FullName; }
                    ImGui.EndPopup();
                }

                if (ImGui.IsItemClicked())
                {
                    SelectedFile = file.FullName;
                }
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    var extension = file.Extension.ToLowerInvariant();
                    if (extension == ".dmm" || extension == ".json")
                    {
                        _editorContext.OpenScene(file.FullName);
                    }
                    // TODO: Open script editor for scripts
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

        private void DeleteItem(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
