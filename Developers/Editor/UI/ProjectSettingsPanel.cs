using ImGuiNET;
using Shared.Config;
using Core.Graphics;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Shared.Interfaces;
using Shared;

namespace Editor.UI
{
    public class ProjectSettingsPanel : IUiPanel
    {
        public string Name => "Project Settings";
        public bool IsOpen { get; set; } = false;

        private readonly IConfigurationManager _manager;
        private List<CVarInfo>? _cachedServerCVars;
        private List<CVarInfo>? _cachedClientCVars;
        private List<CVarInfo>? _cachedAllCVars;

        public ProjectSettingsPanel(IConfigurationManager manager)
        {
            _manager = manager;
            _manager.RegisterFromAssemblies(typeof(ConfigKeys).Assembly);
            _manager.AddProvider(new JsonConfigProvider("project_config.json"));
            _manager.LoadAll();
            _manager.OnCVarChanged += (_, _) => ClearCache();
        }

        private void ClearCache()
        {
            _cachedServerCVars = null;
            _cachedClientCVars = null;
            _cachedAllCVars = null;
        }

        public void Draw()
        {
            if (!IsOpen)
                return;

            bool isOpen = IsOpen;
            if (ImGui.Begin(Name, ref isOpen))
            {
                if (ImGui.BeginTabBar("ProjectSettingsTabs"))
                {
                    var cvars = _manager.GetRegisteredCVars();
                    var categories = cvars
                        .Select(c => c.Category)
                        .Distinct()
                        .OrderBy(c => c);

                    foreach (var category in categories)
                    {
                        if (ImGui.BeginTabItem(category))
                        {
                            DrawCategoryCVars(category);
                            ImGui.EndTabItem();
                        }
                    }

                    if (ImGui.BeginTabItem("All"))
                    {
                        DrawFilteredCVars(ref _cachedAllCVars, CVarFlags.None);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }

                ImGui.Separator();
                if (ImGui.Button("Save Project Configuration"))
                {
                    _manager.SaveAll();
                }
                ImGui.End();
            }
            IsOpen = isOpen;
        }

        private void DrawCategoryCVars(string category)
        {
            var cvars = _manager.GetRegisteredCVars()
                .Where(c => c.Category == category)
                .OrderBy(c => c.Name);

            foreach (var info in cvars)
            {
                DrawCVarEditor(info);
            }
        }

        private void DrawFilteredCVars(ref List<CVarInfo>? cache, CVarFlags filter)
        {
            if (cache == null)
            {
                var query = _manager.GetRegisteredCVars();
                if (filter != CVarFlags.None)
                {
                    query = query.Where(c => (c.Flags & filter) != 0);
                }
                cache = query.OrderBy(c => c.Name).ToList();
            }

            foreach (var info in cache)
            {
                DrawCVarEditor(info);
            }
        }

        private void DrawCVarEditor(CVarInfo info)
        {
            if (!CVarUiRegistry.TryDraw(info, _manager))
            {
                ImGui.Text($"{info.Name}: {info.Value} (Unsupported UI)");
            }

            if (!string.IsNullOrEmpty(info.Description))
            {
                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(info.Description);
                }
            }
        }
    }
}
