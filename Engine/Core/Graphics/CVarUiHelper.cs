using ImGuiNET;
using Shared.Config;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System;
using System.Threading.Tasks;

namespace Core.Graphics;

public static class CVarUiHelper
{
    public static void DrawCVarEditor(CVarInfo info, IConfigurationManager manager)
    {
        if (!CVarUiRegistry.TryDraw(info, manager))
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

    public static void DrawConsole(List<string> consoleOutput, ref string consoleInput, IConsoleCommandManager commandManager)
    {
        var height = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() - 10;
        ImGui.BeginChild("ConsoleOutput", new Vector2(0, height), ImGuiChildFlags.None, ImGuiWindowFlags.None);

        string[] outputCopy;
        lock (consoleOutput)
        {
            outputCopy = consoleOutput.ToArray();
        }

        foreach (var line in outputCopy)
        {
            ImGui.TextUnformatted(line);
        }
        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1.0f);
        ImGui.EndChild();

        ImGui.PushItemWidth(-1);
        bool reclaim_focus = false;
        if (ImGui.InputText("##ConsoleInput", ref consoleInput, 256, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            var input = consoleInput;
            consoleInput = "";
            if (!string.IsNullOrWhiteSpace(input))
            {
                lock(consoleOutput) {
                    consoleOutput.Add($"> {input}");
                }

                Task.Run(async () => {
                    var result = await commandManager.ExecuteCommand(input);
                    lock(consoleOutput) {
                        consoleOutput.AddRange(result.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                    }
                });
            }
            reclaim_focus = true;
        }
        ImGui.PopItemWidth();

        ImGui.SetItemDefaultFocus();
        if (reclaim_focus)
            ImGui.SetKeyboardFocusHere(-1);
    }
}
