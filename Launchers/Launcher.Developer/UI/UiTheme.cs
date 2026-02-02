using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using ImGuiNET;
using System.Numerics;

namespace Launcher.UI
{
    public static class UiTheme
    {
        public static void Apply()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;

            style.WindowRounding = 6.0f;
            style.FrameRounding = 4.0f;
            style.PopupRounding = 4.0f;
            style.ScrollbarRounding = 12.0f;
            style.GrabRounding = 4.0f;
            style.TabRounding = 4.0f;
            style.WindowBorderSize = 1.0f;
            style.FrameBorderSize = 0.0f;
            style.ItemSpacing = new Vector2(8, 8);
            style.WindowPadding = new Vector2(12, 12);

            // Dark theme with blue accents
            colors[(int)ImGuiCol.Text] = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
            colors[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.06f, 0.07f, 1.00f);
            colors[(int)ImGuiCol.ChildBg] = new Vector4(0.09f, 0.09f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.PopupBg] = new Vector4(0.11f, 0.11f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.Border] = new Vector4(0.20f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.FrameBg] = new Vector4(0.15f, 0.15f, 0.17f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.22f, 0.22f, 0.24f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.28f, 0.28f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.TitleBg] = new Vector4(0.05f, 0.05f, 0.06f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.08f, 0.08f, 0.10f, 1.00f);
            colors[(int)ImGuiCol.Button] = new Vector4(0.18f, 0.38f, 0.65f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.25f, 0.50f, 0.80f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.30f, 0.60f, 0.95f, 1.00f);
            colors[(int)ImGuiCol.Header] = new Vector4(0.15f, 0.35f, 0.60f, 0.40f);
            colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.15f, 0.35f, 0.60f, 0.70f);
            colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.15f, 0.35f, 0.60f, 1.00f);
            colors[(int)ImGuiCol.Separator] = colors[(int)ImGuiCol.Border];
            colors[(int)ImGuiCol.CheckMark] = new Vector4(0.20f, 0.45f, 0.75f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.20f, 0.45f, 0.75f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.25f, 0.55f, 0.90f, 1.00f);
        }
    }
}
