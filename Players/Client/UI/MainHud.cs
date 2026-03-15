using Shared.Enums;
using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;
using Shared;

namespace Client.UI
{
    public class MainHud
    {
        private List<string> _chatMessages = new List<string>();
        private string _chatInput = "";

        public void AddMessage(string message)
        {
            _chatMessages.Add(message);
            if (_chatMessages.Count > 100)
                _chatMessages.RemoveAt(0);
        }

        public void Draw(GameObject? player)
        {
            DrawChat();
            DrawStats(player);
            DrawVerbs();
        }

        private void DrawChat()
        {
            var io = ImGui.GetIO();
            float width = 450;
            float height = 250;
            ImGui.SetNextWindowPos(new Vector2(10, io.DisplaySize.Y - height - 10), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0.4f));
            if (ImGui.Begin("Chat", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.BeginChild("ScrollingRegion", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
                foreach (var msg in _chatMessages)
                {
                    if (msg.StartsWith("[You]"))
                        ImGui.TextColored(new Vector4(0.4f, 0.8f, 0.4f, 1.0f), msg);
                    else
                        ImGui.TextUnformatted(msg);
                }
                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                    ImGui.SetScrollHereY(1.0f);
                ImGui.EndChild();

                ImGui.Separator();

                ImGui.PushItemWidth(-1);
                if (ImGui.InputText("##Input", ref _chatInput, 256, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (!string.IsNullOrWhiteSpace(_chatInput))
                    {
                        AddMessage($"[You]: {_chatInput}");
                        _chatInput = "";
                    }
                    ImGui.SetKeyboardFocusHere(-1);
                }
                ImGui.PopItemWidth();
            }
            ImGui.End();
            ImGui.PopStyleColor();
        }

        private void DrawStats(GameObject? player)
        {
            var io = ImGui.GetIO();
            float width = 250;
            float height = 180;
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X - width - 10, 10), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            if (ImGui.Begin("Stats", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.TextColored(new Vector4(0.2f, 0.4f, 0.8f, 1.0f), "CHARACTER STATS");
                ImGui.Separator();
                ImGui.Spacing();

                if (player != null)
                {
                    if (ImGui.BeginTable("StatsTable", 2))
                    {
                        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 60);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.TextDisabled("ID:");
                        ImGui.TableNextColumn(); ImGui.Text($"{player.Id}");

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.TextDisabled("Pos:");
                        ImGui.TableNextColumn(); ImGui.Text($"{player.X}, {player.Y}");

                        ImGui.EndTable();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    var health = player.GetVariable("health");
                    float healthVal = health.Type == DreamValueType.Float ? health.AsFloat() : 100.0f;

                    Vector4 healthColor = healthVal > 50 ? new Vector4(0.3f, 0.8f, 0.3f, 1.0f) : new Vector4(0.8f, 0.3f, 0.3f, 1.0f);
                    ImGui.PushStyleColor(ImGuiCol.PlotHistogram, healthColor);
                    ImGui.ProgressBar(healthVal / 100.0f, new Vector2(-1, 25), $"HEALTH: {healthVal}%");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.TextDisabled("No active player session.");
                }
            }
            ImGui.End();
        }

        private void DrawVerbs()
        {
            var io = ImGui.GetIO();
            float width = 250;
            float height = 200;
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X - width - 10, 200), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            if (ImGui.Begin("Verbs", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
            {
                ImGui.TextColored(new Vector4(0.2f, 0.4f, 0.8f, 1.0f), "ACTIONS");
                ImGui.Separator();
                ImGui.Spacing();

                if (ImGui.BeginTable("VerbsGrid", 2, ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("C1", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("C2", ImGuiTableColumnFlags.WidthStretch);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Ping", new Vector2(-1, 40)))
                    {
                        AddMessage("Sending ping to server...");
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Button("Wave", new Vector2(-1, 40)))
                    {
                         AddMessage("You wave your hand.");
                    }

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (ImGui.Button("Dance", new Vector2(-1, 40)))
                    {
                         AddMessage("You start dancing!");
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Button("Help", new Vector2(-1, 40)))
                    {
                         AddMessage("Requesting help...");
                    }

                    ImGui.EndTable();
                }
            }
            ImGui.End();
        }
    }
}
