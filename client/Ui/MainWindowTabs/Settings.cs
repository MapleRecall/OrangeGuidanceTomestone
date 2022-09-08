using System.Numerics;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class Settings : ITab {
    public string Name => "Settings";

    private Plugin Plugin { get; }
    private int _tab;
    private string _extraCode = string.Empty;
    private List<(uint, string)> Territories { get; }

    private delegate void DrawSettingsDelegate(ref bool anyChanged, ref bool vfx);

    private IReadOnlyList<(string, DrawSettingsDelegate)> Tabs { get; }

    internal Settings(Plugin plugin) {
        this.Plugin = plugin;

        this.Territories = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()!
            .Where(row => row.RowId != 0)
            .Select(row => (row.RowId, row.PlaceName.Value?.Name?.ToDalamudString().TextValue))
            .Where(entry => entry.TextValue != null && !string.IsNullOrWhiteSpace(entry.TextValue))
            .ToList()!;

        this.Tabs = new List<(string, DrawSettingsDelegate)> {
            ("General", this.DrawGeneral),
            ("Writer", this.DrawWriter),
            ("Viewer", this.DrawViewer),
            ("Unlocks", this.DrawUnlocks),
            ("Account", this.DrawAccount),
        };
    }

    public void Dispose() {
    }

    public void Draw() {
        ImGui.PushTextWrapPos();

        var anyChanged = false;
        var vfx = false;

        var widestTabName = this.Tabs
            .Select(entry => ImGui.CalcTextSize(entry.Item1).X)
            .Max();

        var leftOver = ImGui.GetContentRegionAvail().X - widestTabName - ImGui.GetStyle().ItemSpacing.X - ImGui.GetStyle().FrameBorderSize;
        var childHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y * 2;
        if (ImGui.BeginTable("##settings-tabs", 2)) {
            ImGui.TableSetupColumn("##names", ImGuiTableColumnFlags.None, widestTabName + ImGui.GetStyle().ItemSpacing.X);
            ImGui.TableSetupColumn("##content", ImGuiTableColumnFlags.None, leftOver);

            ImGui.TableNextRow();

            if (ImGui.TableSetColumnIndex(0)) {
                for (var i = 0; i < this.Tabs.Count; i++) {
                    var (name, _) = this.Tabs[i];
                    if (ImGui.Selectable($"{name}##tab-{i}", i == this._tab)) {
                        this._tab = i;
                    }
                }
            }

            if (ImGui.TableSetColumnIndex(1)) {
                if (ImGui.BeginChild("##tab-content-child", new Vector2(-1, childHeight))) {
                    var (_, draw) = this.Tabs[this._tab];
                    draw(ref anyChanged, ref vfx);
                }

                ImGui.EndChild();
            }

            ImGui.EndTable();
        }

        if (anyChanged) {
            this.Plugin.SaveConfig();
        }

        if (vfx) {
            this.Plugin.Messages.RemoveVfx();
            this.Plugin.Messages.Clear();
            this.Plugin.Messages.SpawnVfx();
        }

        ImGui.PopTextWrapPos();
    }

    private void DrawGeneral(ref bool anyChanged, ref bool vfx) {
        anyChanged |= vfx |= ImGui.Checkbox("Disable in trials", ref this.Plugin.Config.DisableTrials);
        anyChanged |= vfx |= ImGui.Checkbox("Disable in Deep Dungeons", ref this.Plugin.Config.DisableDeepDungeon);
        anyChanged |= vfx |= ImGui.Checkbox("Remove glow effect from signs", ref this.Plugin.Config.RemoveGlow);

        var tt = this.Plugin.DataManager.GetExcelSheet<TerritoryType>();
        if (tt == null) {
            return;
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Ban list");
        ImGui.TreePush();
        ImGui.TextUnformatted("Click to ban or unban.");
        ImGui.TreePop();

        if (ImGui.BeginChild("##ban-list", new Vector2(-1, -1), true)) {
            var toAdd = -1L;
            var toRemove = -1L;
            foreach (var bannedId in this.Plugin.Config.BannedTerritories) {
                var territory = tt.GetRow(bannedId)?.PlaceName.Value?.Name?.ToDalamudString().TextValue ?? $"{bannedId}";
                if (ImGui.Selectable($"{territory}##{bannedId}", true)) {
                    toRemove = bannedId;
                }
            }

            ImGui.Separator();

            var clipper = ImGuiExt.Clipper(this.Territories.Count - this.Plugin.Config.BannedTerritories.Count);
            while (clipper.Step()) {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var (rowId, name) = this.Territories[i];
                    if (this.Plugin.Config.BannedTerritories.Contains(rowId)) {
                        continue;
                    }

                    if (ImGui.Selectable($"{name}##{rowId}")) {
                        toAdd = rowId;
                    }
                }
            }

            if (toRemove > -1) {
                this.Plugin.Config.BannedTerritories.Remove((uint) toRemove);
            }

            if (toAdd > -1) {
                this.Plugin.Config.BannedTerritories.Add((uint) toAdd);
            }

            if (toRemove > -1 || toAdd > -1) {
                this.Plugin.SaveConfig();
            }
        }

        ImGui.EndChild();
    }

    private void DrawWriter(ref bool anyChanged, ref bool vfx) {
        if (ImGui.Button("Refresh packs")) {
            Pack.UpdatePacks();
        }

        var glyph = this.Plugin.Config.DefaultGlyph + 1;
        if (ImGui.InputInt("Default glyph", ref glyph)) {
            this.Plugin.Config.DefaultGlyph = Math.Min(4, Math.Max(0, glyph - 1));
            anyChanged = true;
        }
    }

    private void DrawViewer(ref bool anyChanged, ref bool vfx) {
        anyChanged |= ImGui.SliderFloat("Viewer opacity", ref this.Plugin.Config.ViewerOpacity, 0f, 100.0f, $"{this.Plugin.Config.ViewerOpacity:N3}%%");
        anyChanged |= ImGui.Checkbox("Open the viewer automatically when near a sign", ref this.Plugin.Config.AutoViewer);
        anyChanged |= ImGui.Checkbox("Close the viewer automatically when no signs are nearby", ref this.Plugin.Config.AutoViewerClose);

        if (this.Plugin.Config.AutoViewerClose) {
            ImGui.TreePush();
            anyChanged |= ImGui.Checkbox("Hide viewer titlebar", ref this.Plugin.Config.HideTitlebar);
            ImGui.TreePop();
        }

        anyChanged |= ImGui.Checkbox("Lock viewer in place", ref this.Plugin.Config.LockViewer);
        anyChanged |= ImGui.Checkbox("Click through viewer", ref this.Plugin.Config.ClickThroughViewer);
    }

    private void DrawUnlocks(ref bool anyChanged, ref bool vfx) {
        this.ExtraCodeInput();
    }

    private void DrawAccount(ref bool anyChanged, ref bool vfx) {
        this.DeleteAccountButton();
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
                    this.Plugin.Ui.ShowModal($"Code claimed.\n\nYou can now post up to {10 + extra:N0} messages.");
                } else {
                    this.Plugin.Ui.ShowModal("Code claimed but the server gave an unexpected response.");
                }
            } else {
                this.Plugin.Ui.ShowModal("Invalid code.");
            }
        });
    }

    private void DeleteAccountButton() {
        var ctrl = ImGui.GetIO().KeyCtrl;
        if (!ctrl) {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Delete account")) {
            Task.Run(async () => {
                var resp = await ServerHelper.SendRequest(
                    this.Plugin.Config.ApiKey,
                    HttpMethod.Delete,
                    "/account"
                );

                if (resp.IsSuccessStatusCode) {
                    this.Plugin.Config.ApiKey = string.Empty;
                    this.Plugin.SaveConfig();
                }
            });
        }

        if (!ctrl) {
            ImGui.EndDisabled();
        }

        ImGui.SameLine();
        ImGuiExt.HelpIcon("Hold Ctrl to enable delete button.");

        ImGui.TextUnformatted("This will delete all your messages and votes.");
    }
}
