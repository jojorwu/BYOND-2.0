using ImGuiNET;
using Shared;

namespace Editor.UI
{
    public class SettingsPanel : IUiPanel
    {
        public string Name => "Settings";
        public bool IsOpen { get; set; } = false;

        private readonly IEditorSettingsManager _settingsManager;

        public SettingsPanel(IEditorSettingsManager settingsManager)
        {
            _settingsManager = settingsManager;
        }

        public void Draw()
        {
            if (!IsOpen)
                return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 400), ImGuiCond.FirstUseEver);
            bool isOpen = IsOpen;
            if (ImGui.Begin(Name, ref isOpen))
            {
                var settings = _settingsManager.Settings;

                if (ImGui.BeginTabBar("SettingsTabs"))
                {
                    if (ImGui.BeginTabItem("General"))
                    {
                        if (ImGui.BeginTable("GeneralSettingsTable", 2))
                        {
                            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 150);
                            ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);

                            DrawSettingRow("Server Executable", () => {
                                string serverPath = settings.ServerExecutablePath;
                                if (ImGui.InputText("##ServerExecutable", ref serverPath, 260)) settings.ServerExecutablePath = serverPath;
                            });

                            DrawSettingRow("Client Executable", () => {
                                string clientPath = settings.ClientExecutablePath;
                                if (ImGui.InputText("##ClientExecutable", ref clientPath, 260)) settings.ClientExecutablePath = clientPath;
                            });

                            DrawSettingRow("Use Dark Theme", () => {
                                bool useDarkTheme = settings.UseDarkTheme;
                                if (ImGui.Checkbox("##UseDarkTheme", ref useDarkTheme)) settings.UseDarkTheme = useDarkTheme;
                            });

                            DrawSettingRow("Font Size", () => {
                                int fontSize = settings.FontSize;
                                if (ImGui.InputInt("##FontSize", ref fontSize)) settings.FontSize = fontSize;
                            });

                            DrawSettingRow("Auto Save", () => {
                                bool autoSave = settings.AutoSave;
                                if (ImGui.Checkbox("##AutoSave", ref autoSave)) settings.AutoSave = autoSave;
                            });

                            if (settings.AutoSave)
                            {
                                DrawSettingRow("Interval (min)", () => {
                                    int interval = settings.AutoSaveIntervalMinutes;
                                    if (ImGui.InputInt("##AutoSaveInterval", ref interval)) settings.AutoSaveIntervalMinutes = interval;
                                });
                            }

                            ImGui.EndTable();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Graphics"))
                    {
                        if (ImGui.BeginTable("GraphicsSettingsTable", 2))
                        {
                            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 150);
                            ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);

                            DrawSettingRow("Backend", () => {
                                string[] backends = { "OpenGL", "Vulkan" };
                                int currentIdx = System.Array.IndexOf(backends, settings.GraphicsBackend);
                                if (ImGui.Combo("##Backend", ref currentIdx, backends, backends.Length)) settings.GraphicsBackend = backends[currentIdx];
                            });

                            DrawSettingRow("Resolution X", () => {
                                int resX = settings.ResolutionX;
                                if (ImGui.InputInt("##ResX", ref resX)) settings.ResolutionX = resX;
                            });

                            DrawSettingRow("Resolution Y", () => {
                                int resY = settings.ResolutionY;
                                if (ImGui.InputInt("##ResY", ref resY)) settings.ResolutionY = resY;
                            });

                            DrawSettingRow("VSync", () => {
                                bool vsync = settings.VSync;
                                if (ImGui.Checkbox("##VSync", ref vsync)) settings.VSync = vsync;
                            });

                            DrawSettingRow("Enable SSAO", () => {
                                bool ssao = settings.EnableSsao;
                                if (ImGui.Checkbox("##SSAO", ref ssao)) settings.EnableSsao = ssao;
                            });

                            DrawSettingRow("Enable Bloom", () => {
                                bool bloom = settings.EnableBloom;
                                if (ImGui.Checkbox("##Bloom", ref bloom)) settings.EnableBloom = bloom;
                            });

                            ImGui.EndTable();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Viewport"))
                    {
                        if (ImGui.BeginTable("ViewportSettingsTable", 2))
                        {
                            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 150);
                            ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);

                            DrawSettingRow("Show Grid", () => {
                                bool showGrid = settings.ShowGrid;
                                if (ImGui.Checkbox("##ShowGrid", ref showGrid)) settings.ShowGrid = showGrid;
                            });

                            DrawSettingRow("Grid Size", () => {
                                int gridSize = settings.GridSize;
                                if (ImGui.InputInt("##GridSize", ref gridSize)) settings.GridSize = gridSize;
                            });

                            DrawSettingRow("Grid Color", () => {
                                var gridColor = settings.GridColor;
                                if (ImGui.ColorEdit4("##GridColor", ref gridColor)) settings.GridColor = gridColor;
                            });

                            DrawSettingRow("Snap To Grid", () => {
                                bool snapToGrid = settings.SnapToGrid;
                                if (ImGui.Checkbox("##SnapToGrid", ref snapToGrid)) settings.SnapToGrid = snapToGrid;
                            });

                            ImGui.EndTable();
                        }
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("API"))
                    {
                         if (ImGui.BeginTable("ApiSettingsTable", 2))
                        {
                            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 150);
                            ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);

                            DrawSettingRow("Target Scripting API", () => {
                                string[] apis = { "Lua", "C#", "DM" };
                                int currentIdx = System.Array.IndexOf(apis, settings.TargetApi);
                                if (ImGui.Combo("##TargetApi", ref currentIdx, apis, apis.Length)) settings.TargetApi = apis[currentIdx];
                            });

                            ImGui.EndTable();
                        }
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.Separator();
                if (ImGui.Button("Save", new System.Numerics.Vector2(100, 30)))
                {
                    _settingsManager.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Close", new System.Numerics.Vector2(100, 30)))
                {
                    isOpen = false;
                }
            }
            ImGui.End();
            IsOpen = isOpen;
        }

        private void DrawSettingRow(string label, System.Action drawEditor)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(label);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            drawEditor();
        }
    }
}
