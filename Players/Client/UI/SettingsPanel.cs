using ImGuiNET;
using Shared.Config;
using Core.Graphics;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace Client.UI
{
    public class SettingsPanel
    {
        public string Name => "Settings";
        public bool IsOpen = false;

        private readonly IConfigurationManager _manager;
        private readonly IConsoleCommandManager _commandManager;
        private List<string>? _cachedCategories;
        private List<CVarInfo>? _cachedAllCVars;
        private readonly Dictionary<string, List<CVarInfo>> _cachedCategoryCVars = new();
        private string _consoleInput = "";
        private List<string> _consoleOutput = new();

        public SettingsPanel(IConfigurationManager manager, IConsoleCommandManager commandManager)
        {
            _manager = manager;
            _commandManager = commandManager;
            _manager.OnCVarChanged += (_, _) => { _cachedCategories = null; _cachedAllCVars = null; _cachedCategoryCVars.Clear(); };
        }

        public void Draw()
        {
            if (!IsOpen)
                return;

            ImGui.SetNextWindowSize(new Vector2(600, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin(Name, ref IsOpen))
            {
                if (ImGui.BeginTabBar("SettingsTabs"))
                {
                    if (_cachedCategories == null)
                    {
                         _cachedCategories = _manager.GetRegisteredCVars()
                            .Where(c => (c.Flags & CVarFlags.Client) != 0 || c.Category != "General")
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
                if (ImGui.Button("Save Configuration"))
                {
                    _manager.SaveAll();
                }
                ImGui.SameLine();
                if (ImGui.Button("Close"))
                {
                    IsOpen = false;
                }
            }
            ImGui.End();
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

            DrawCVarTable(cvars);
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

            DrawCVarTable(cache);
        }

        private void DrawCVarTable(List<CVarInfo> cvars)
        {
            if (ImGui.BeginTable("CVarTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var info in cvars)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.TextUnformatted(info.Name);
                    if (!string.IsNullOrEmpty(info.Description))
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled("(?)");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(info.Description);
                        }
                    }

                    ImGui.TableNextColumn();
                    if (!CVarUiRegistry.TryDraw(info, _manager))
                    {
                        ImGui.Text($"{info.Value} (Unsupported)");
                    }
                }
                ImGui.EndTable();
            }
        }

        private void DrawConsole()
        {
            CVarUiHelper.DrawConsole(_consoleOutput, ref _consoleInput, _commandManager);
        }

        private void DrawCVarEditor(CVarInfo info)
        {
            CVarUiRegistry.TryDraw(info, _manager);
        }
    }
}
