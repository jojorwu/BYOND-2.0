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
        private readonly IConsoleCommandManager _commandManager;
        private List<string>? _cachedCategories;
        private List<CVarInfo>? _cachedAllCVars;
        private readonly Dictionary<string, List<CVarInfo>> _cachedCategoryCVars = new();
        private string _consoleInput = "";
        private List<string> _consoleOutput = new();

        public ProjectSettingsPanel(IConfigurationManager manager, IConsoleCommandManager commandManager)
        {
            _manager = manager;
            _commandManager = commandManager;
            _manager.RegisterFromAssemblies(typeof(ConfigKeys).Assembly);
            _manager.AddProvider(new JsonConfigProvider("project_config.json"));
            _manager.LoadAll();
            _manager.OnCVarChanged += (_, _) => ClearCache();
        }

        private void ClearCache()
        {
            _cachedCategories = null;
            _cachedAllCVars = null;
            _cachedCategoryCVars.Clear();
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
                    if (_cachedCategories == null)
                    {
                        _cachedCategories = _manager.GetRegisteredCVars()
                            .Select(c => c.Category)
                            .Distinct()
                            .OrderBy(c => c)
                            .ToList();
                    }

                    foreach (var category in _cachedCategories)
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
                    if (ImGui.BeginTabItem("Console"))
                    {
                        DrawConsole();
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
            if (!_cachedCategoryCVars.TryGetValue(category, out var cvars))
            {
                cvars = _manager.GetRegisteredCVars()
                    .Where(c => c.Category == category)
                    .OrderBy(c => c.Name)
                    .ToList();
                _cachedCategoryCVars[category] = cvars;
            }

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

        private void DrawConsole()
        {
            CVarUiHelper.DrawConsole(_consoleOutput, ref _consoleInput, _commandManager);
        }

        private void DrawCVarEditor(CVarInfo info)
        {
            CVarUiHelper.DrawCVarEditor(info, _manager);
        }
    }
}
