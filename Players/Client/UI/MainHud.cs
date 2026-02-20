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
            float width = 400;
            float height = 200;
            ImGui.SetNextWindowPos(new Vector2(10, io.DisplaySize.Y - height - 10), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            ImGui.Begin("Chat", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove);

            ImGui.BeginChild("ScrollingRegion", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
            foreach (var msg in _chatMessages)
            {
                ImGui.TextUnformatted(msg);
            }
            if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
                ImGui.SetScrollHereY(1.0f);
            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.InputText("##Input", ref _chatInput, 256, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrWhiteSpace(_chatInput))
                {
                    // For now, just add locally as a test
                    AddMessage($"[You]: {_chatInput}");
                    _chatInput = "";
                }
                ImGui.SetKeyboardFocusHere(-1);
            }

            ImGui.End();
        }

        private void DrawStats(GameObject? player)
        {
            var io = ImGui.GetIO();
            float width = 200;
            float height = 150;
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X - width - 10, 10), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            ImGui.Begin("Stats", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Character Stats");
            ImGui.Separator();

            if (player != null)
            {
                ImGui.Text($"ID: {player.Id}");
                ImGui.Text($"Pos: ({player.X}, {player.Y}, {player.Z})");

                // Try to get some custom vars if they exist
                var health = player.GetVariable("health");
                if (health.Type == DreamValueType.Float)
                    ImGui.ProgressBar(health.AsFloat() / 100.0f, new Vector2(-1, 0), $"Health: {health.AsFloat()}%");
                else
                    ImGui.ProgressBar(1.0f, new Vector2(-1, 0), "Health: 100%");
            }
            else
            {
                ImGui.Text("No player object found.");
            }

            ImGui.End();
        }

        private void DrawVerbs()
        {
            var io = ImGui.GetIO();
            float width = 200;
            float height = 200;
            ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X - width - 10, 170), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);

            ImGui.Begin("Verbs", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Available Actions");
            ImGui.Separator();

            if (ImGui.Button("Ping Server", new Vector2(-1, 30)))
            {
                // Action triggered
                AddMessage("Sending ping to server...");
            }

            if (ImGui.Button("Emote: Wave", new Vector2(-1, 30)))
            {
                 AddMessage("You wave your hand.");
            }

            ImGui.End();
        }
    }
}
