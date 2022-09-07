using Dalamud.Interface;
using ImGuiNET;

namespace OrangeGuidanceTomestone.Ui;

internal static class ImGuiExt {
    private static bool InternalIconButton(Func<string, bool> func, FontAwesomeIcon icon, string? id = null) {
        var label = icon.ToIconString();
        if (id != null) {
            label += $"##{id}";
        }

        ImGui.PushFont(UiBuilder.IconFont);
        var ret = func(label);
        ImGui.PopFont();

        return ret;
    }

    internal static bool SmallIconButton(FontAwesomeIcon icon, string? id = null) {
        return InternalIconButton(ImGui.SmallButton, icon, id);
    }

    internal static bool IconButton(FontAwesomeIcon icon, string? id = null) {
        return InternalIconButton(ImGui.Button, icon, id);
    }

    internal static void HelpIcon(string text) {
        var colour = ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled];
        ImGui.PushStyleColor(ImGuiCol.Text, colour);
        ImGui.TextUnformatted("(?)");
        ImGui.PopStyleColor();

        if (!ImGui.IsItemHovered()) {
            return;
        }

        var width = ImGui.CalcTextSize("m") * 40;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(width.X);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
