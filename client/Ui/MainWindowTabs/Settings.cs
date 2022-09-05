using ImGuiNET;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class Settings : ITab {
    public string Name => "Settings";

    private Plugin Plugin { get; }
    private string _extraCode = string.Empty;

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

        this.ExtraCodeInput();
    }

    private void ExtraCodeInput() {
        ImGui.InputText("Extra code", ref this._extraCode, 128);
        if (!ImGui.Button("Claim")) {
            return;
        }

        var code = this._extraCode;
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Post,
                "/claim",
                null,
                new StringContent(code)
            );

            if (resp.IsSuccessStatusCode) {
                this._extraCode = string.Empty;
                var text = await resp.Content.ReadAsStringAsync();
                if (uint.TryParse(text, out var extra)) {
                    this.Plugin.Ui.MainWindow.ExtraMessages = extra;
                    this.Plugin.Ui.AddModal($"Code claimed.\n\nYou can now post up to {10 + extra:N0} messages.");
                } else {
                    this.Plugin.Ui.AddModal("Code claimed but the server gave an unexpected response.");
                }
            } else {
                this.Plugin.Ui.AddModal("Invalid code.");
            }
        });
    }
}
