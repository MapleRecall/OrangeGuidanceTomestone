using ImGuiNET;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class Settings : ITab {
    public string Name => "Settings";

    private Plugin Plugin { get; }

    internal Settings(Plugin plugin) {
        this.Plugin = plugin;
    }

    public void Draw() {
        var anyChanged = false;
        var vfx = false;

        anyChanged |= vfx |= ImGui.Checkbox("Disable in trials", ref this.Plugin.Config.DisableTrials);
        anyChanged |= vfx |= ImGui.Checkbox("Disable in Deep Dungeons", ref this.Plugin.Config.DisableDeepDungeon);
        anyChanged |= vfx |= ImGui.Checkbox("Remove glow effect from signs", ref this.Plugin.Config.RemoveGlow);

        if (anyChanged) {
            this.Plugin.SaveConfig();
        }

        if (vfx) {
            this.Plugin.Messages.RemoveVfx();
            this.Plugin.Messages.Clear();
        }
    }
}
