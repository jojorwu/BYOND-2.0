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
        private string _renamingPath = null;
        private string _newName = "";
        private readonly TextureManager _textureManager;
        private readonly uint _folderIcon;
        private readonly uint _fileDefaultIcon;
        private readonly uint _fileImageIcon;
        private readonly uint _fileMapIcon;
        private readonly uint _fileScriptIcon;
        private readonly LocalizationManager _localizationManager;

        public AssetBrowserPanel(Project project, EditorContext editorContext, TextureManager textureManager, LocalizationManager localizationManager)
        {
            _project = project;
            _editorContext = editorContext;
            _textureManager = textureManager;
            _localizationManager = localizationManager;
            _folderIcon = _textureManager.GetTexture("Editor/assets/icons/folder.png");
            _fileDefaultIcon = _textureManager.GetTexture("Editor/assets/icons/file_default.png");
            _fileImageIcon = _textureManager.GetTexture("Editor/assets/icons/file_image.png");
            _fileMapIcon = _textureManager.GetTexture("Editor/assets/icons/file_map.png");
            _fileScriptIcon = _textureManager.GetTexture("Editor/assets/icons/file_script.png");
        }

        public void Draw()
        {
            ImGui.Begin(_localizationManager.GetString("Assets"));

            if (ImGui.BeginPopupContextWindow("AssetBrowserContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.MenuItem(_localizationManager.GetString("New Lua Script"))) CreateNewScript(_project.RootPath, ".lua");
                if (ImGui.MenuItem(_localizationManager.GetString("New DM Script"))) CreateNewScript(_project.RootPath, ".dm");
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
                bool isTreeNodeOpen;
                if (_renamingPath == directory.FullName)
                {
                    ImGui.SetKeyboardFocusHere();
                    if (ImGui.InputText("##rename", ref _newName, 255, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    {
                        string newPath = Path.Combine(directory.Parent.FullName, _newName);
                        try
                        {
                            if (!Directory.Exists(newPath))
                            {
                                Directory.Move(directory.FullName, newPath);
                            }
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine($"[ERROR] Failed to rename directory: {e.Message}");
                        }
                        _renamingPath = null;
                    }
                    isTreeNodeOpen = ImGui.TreeNodeEx(directory.Name, ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.Selected);
                }
                else
                {
                    ImGui.Image((System.IntPtr)_folderIcon, new System.Numerics.Vector2(16, 16));
                    ImGui.SameLine();
                    isTreeNodeOpen = ImGui.TreeNodeEx(directory.Name, ImGuiTreeNodeFlags.OpenOnArrow);
                }


                if (ImGui.BeginPopupContextItem($"ContextMenu_{directory.FullName}"))
                {
                    if (ImGui.MenuItem(_localizationManager.GetString("New Lua Script"))) CreateNewScript(directory.FullName, ".lua");
                    if (ImGui.MenuItem(_localizationManager.GetString("New DM Script"))) CreateNewScript(directory.FullName, ".dm");
                    if (ImGui.MenuItem(_localizationManager.GetString("Rename")))
                    {
                        _renamingPath = directory.FullName;
                        _newName = directory.Name;
                    }
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
                if (_renamingPath == file.FullName)
                {
                    ImGui.SetKeyboardFocusHere();
                    if (ImGui.InputText("##rename", ref _newName, 255, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
                    {
                        string newPath = Path.Combine(file.Directory.FullName, _newName);
                        try
                        {
                            if (!File.Exists(newPath))
                            {
                                File.Move(file.FullName, newPath);
                            }
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine($"[ERROR] Failed to rename file: {e.Message}");
                        }
                        _renamingPath = null;
                    }
                }
                else
                {
                    var icon = GetIconForFile(file.Extension);
                    ImGui.Image((System.IntPtr)icon, new System.Numerics.Vector2(16, 16));
                    ImGui.SameLine();
                    if (ImGui.Selectable(file.Name, false, ImGuiSelectableFlags.AllowDoubleClick))
                    {
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            _editorContext.OpenFile(file.FullName);
                        }
                    }
                }
                if (ImGui.BeginPopupContextItem($"ContextMenu_{file.FullName}"))
                {
                    if (ImGui.MenuItem(_localizationManager.GetString("Rename")))
                    {
                        _renamingPath = file.FullName;
                        _newName = file.Name;
                    }
                    ImGui.EndPopup();
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

        private uint GetIconForFile(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => _fileImageIcon,
                ".dmm" or ".json" => _fileMapIcon,
                ".dm" or ".lua" => _fileScriptIcon,
                _ => _fileDefaultIcon,
            };
        }
    }
}
