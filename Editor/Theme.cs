using ImGuiNET;
using System.Numerics;

namespace Editor
{
    public static class Theme
    {
        public static void ApplyTheme()
        {
            var style = ImGui.GetStyle();
            var colors = style.Colors;

            // A modern dark theme inspired by Dracula and other popular themes.
            colors[(int)ImGuiCol.Text]                   = new Vector4(0.95f, 0.96f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.TextDisabled]           = new Vector4(0.36f, 0.42f, 0.47f, 1.00f);
            colors[(int)ImGuiCol.WindowBg]               = new Vector4(0.11f, 0.12f, 0.13f, 1.00f);
            colors[(int)ImGuiCol.ChildBg]                = new Vector4(0.15f, 0.16f, 0.17f, 1.00f);
            colors[(int)ImGuiCol.PopupBg]                = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
            colors[(int)ImGuiCol.Border]                 = new Vector4(0.08f, 0.10f, 0.12f, 1.00f);
            colors[(int)ImGuiCol.BorderShadow]           = new Vector4(0.06f, 0.06f, 0.06f, 0.00f);
            colors[(int)ImGuiCol.FrameBg]                = new Vector4(0.20f, 0.22f, 0.24f, 1.00f);
            colors[(int)ImGuiCol.FrameBgHovered]         = new Vector4(0.25f, 0.27f, 0.30f, 1.00f);
            colors[(int)ImGuiCol.FrameBgActive]          = new Vector4(0.30f, 0.33f, 0.36f, 1.00f);
            colors[(int)ImGuiCol.TitleBg]                = new Vector4(0.09f, 0.10f, 0.11f, 1.00f);
            colors[(int)ImGuiCol.TitleBgActive]          = new Vector4(0.14f, 0.16f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.TitleBgCollapsed]       = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
            colors[(int)ImGuiCol.MenuBarBg]              = new Vector4(0.15f, 0.16f, 0.18f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarBg]            = new Vector4(0.02f, 0.02f, 0.02f, 0.39f);
            colors[(int)ImGuiCol.ScrollbarGrab]          = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabHovered]   = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
            colors[(int)ImGuiCol.ScrollbarGrabActive]    = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
            colors[(int)ImGuiCol.CheckMark]              = new Vector4(0.56f, 0.80f, 0.98f, 1.00f);
            colors[(int)ImGuiCol.SliderGrab]             = new Vector4(0.34f, 0.34f, 0.34f, 1.00f);
            colors[(int)ImGuiCol.SliderGrabActive]       = new Vector4(0.44f, 0.44f, 0.44f, 1.00f);
            colors[(int)ImGuiCol.Button]                 = new Vector4(0.25f, 0.28f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.ButtonHovered]          = new Vector4(0.30f, 0.33f, 0.37f, 1.00f);
            colors[(int)ImGuiCol.ButtonActive]           = new Vector4(0.35f, 0.39f, 0.43f, 1.00f);
            colors[(int)ImGuiCol.Header]                 = new Vector4(0.20f, 0.22f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.HeaderHovered]          = new Vector4(0.25f, 0.28f, 0.31f, 1.00f);
            colors[(int)ImGuiCol.HeaderActive]           = new Vector4(0.30f, 0.33f, 0.37f, 1.00f);
            colors[(int)ImGuiCol.Separator]              = colors[(int)ImGuiCol.Border];
            colors[(int)ImGuiCol.SeparatorHovered]       = new Vector4(0.41f, 0.42f, 0.44f, 1.00f);
            colors[(int)ImGuiCol.SeparatorActive]        = new Vector4(0.51f, 0.53f, 0.56f, 1.00f);
            colors[(int)ImGuiCol.ResizeGrip]             = new Vector4(0.26f, 0.59f, 0.98f, 0.25f);
            colors[(int)ImGuiCol.ResizeGripHovered]      = new Vector4(0.26f, 0.59f, 0.98f, 0.67f);
            colors[(int)ImGuiCol.ResizeGripActive]       = new Vector4(0.26f, 0.59f, 0.98f, 0.95f);
            colors[(int)ImGuiCol.Tab]                    = new Vector4(0.18f, 0.20f, 0.22f, 1.00f);
            colors[(int)ImGuiCol.TabHovered]             = new Vector4(0.28f, 0.31f, 0.34f, 1.00f);
            colors[35]              = new Vector4(0.24f, 0.26f, 0.29f, 1.00f); // TabActive
            colors[36]           = new Vector4(0.16f, 0.17f, 0.18f, 1.00f); // TabUnfocused
            colors[37]     = new Vector4(0.21f, 0.23f, 0.25f, 1.00f); // TabUnfocusedActive
            colors[(int)ImGuiCol.DockingPreview]         = new Vector4(0.40f, 0.60f, 0.80f, 0.70f);
            colors[(int)ImGuiCol.DockingEmptyBg]         = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
            colors[(int)ImGuiCol.PlotLines]              = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
            colors[(int)ImGuiCol.PlotLinesHovered]       = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogram]          = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.PlotHistogramHovered]   = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
            colors[(int)ImGuiCol.TableHeaderBg]          = new Vector4(0.19f, 0.20f, 0.21f, 1.00f);
            colors[(int)ImGuiCol.TableBorderStrong]      = new Vector4(0.31f, 0.31f, 0.35f, 1.00f);
            colors[(int)ImGuiCol.TableBorderLight]       = new Vector4(0.23f, 0.23f, 0.25f, 1.00f);
            colors[(int)ImGuiCol.TableRowBg]             = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
            colors[(int)ImGuiCol.TableRowBgAlt]          = new Vector4(1.00f, 1.00f, 1.00f, 0.06f);
            colors[(int)ImGuiCol.TextSelectedBg]         = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
            colors[(int)ImGuiCol.DragDropTarget]         = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
            colors[49]           = new Vector4(0.26f, 0.59f, 0.98f, 1.00f); // NavHighlight
            colors[(int)ImGuiCol.NavWindowingHighlight]  = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
            colors[(int)ImGuiCol.NavWindowingDimBg]      = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
            colors[(int)ImGuiCol.ModalWindowDimBg]       = new Vector4(0.20f, 0.20f, 0.20f, 0.35f);

            style.WindowPadding = new Vector2(8, 8);
            style.FramePadding = new Vector2(4, 3);
            style.CellPadding = new Vector2(4, 2);
            style.ItemSpacing = new Vector2(8, 4);
            style.ItemInnerSpacing = new Vector2(4, 4);
            style.TouchExtraPadding = new Vector2(0, 0);
            style.IndentSpacing = 21;
            style.ScrollbarSize = 14;
            style.GrabMinSize = 10;

            style.WindowBorderSize = 1;
            style.ChildBorderSize = 1;
            style.PopupBorderSize = 1;
            style.FrameBorderSize = 1;
            style.TabBorderSize = 1;

            style.WindowRounding = 4;
            style.ChildRounding = 4;
            style.FrameRounding = 4;
            style.PopupRounding = 4;
            style.ScrollbarRounding = 9;
            style.GrabRounding = 4;
            style.TabRounding = 4;
        }
    }
}
