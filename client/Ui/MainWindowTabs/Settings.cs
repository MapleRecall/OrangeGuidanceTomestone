using System.Globalization;
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
    private IReadOnlyList<(uint, string)> Territories { get; }
    private List<(uint, bool, string)> FilteredTerritories { get; set; }

    private delegate void DrawSettingsDelegate(ref bool anyChanged, ref bool vfx);

    private IReadOnlyList<(string, DrawSettingsDelegate)> Tabs { get; }

    private string _filter = string.Empty;

    internal Settings(Plugin plugin) {
        this.Plugin = plugin;

        this.Territories = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()!
            .Where(row => row.RowId != 0)
            .Select(row => (row.RowId, row.PlaceName.Value?.Name?.ToDalamudString().TextValue))
            .Where(entry => entry.TextValue != null && !string.IsNullOrWhiteSpace(entry.TextValue))
            .ToList()!;
        this.FilterTerritories(null);

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

    private void FilterTerritories(string? text) {
        var filter = !string.IsNullOrWhiteSpace(text);

        var territories = this.Territories
            .Where(terr => !this.Plugin.Config.BannedTerritories.Contains(terr.Item1))
            .Select(terr => (terr.Item1, false, terr.Item2));

        var tt = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()!;
        this.FilteredTerritories = this.Plugin.Config.BannedTerritories
            .OrderBy(terr => terr)
            .Select(terr => (terr, true, tt.GetRow(terr)?.PlaceName.Value?.Name.ToDalamudString().TextValue ?? $"{terr}"))
            .Concat(territories)
            .Where(terr => !filter || CultureInfo.InvariantCulture.CompareInfo.IndexOf(terr.Item3, text!, CompareOptions.OrdinalIgnoreCase) != -1)
            .ToList();
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
        anyChanged |= vfx |= ImGui.Checkbox("Disable in cutscenes", ref this.Plugin.Config.DisableInCutscene);
        anyChanged |= vfx |= ImGui.Checkbox("Disable in /gpose", ref this.Plugin.Config.DisableInGpose);
        anyChanged |= vfx |= ImGui.Checkbox("Remove glow effect from signs", ref this.Plugin.Config.RemoveGlow);

        var tt = this.Plugin.DataManager.GetExcelSheet<TerritoryType>();
        if (tt == null) {
            return;
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Ban list (click to ban or unban)");

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##filter", "Search...", ref this._filter, 128)) {
            this.FilterTerritories(this._filter);
        }

        if (ImGui.BeginChild("##ban-list", new Vector2(-1, -1), true)) {
            var toAdd = -1L;
            var toRemove = -1L;

            var clipper = ImGuiHelper.Clipper(this.FilteredTerritories.Count);
            while (clipper.Step()) {
                for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                    var (terrId, isBanned, name) = this.FilteredTerritories[i];
                    if (isBanned) {
                        this.DrawBannedTerritory(terrId, name, ref toRemove);
                    } else {
                        this.DrawTerritory(terrId, name, ref toAdd);
                    }
                }
            }

            ImGui.Separator();

            if (toRemove > -1) {
                this.Plugin.Config.BannedTerritories.Remove((uint) toRemove);
                if (this.Plugin.ClientState.TerritoryType == toRemove) {
                    this.Plugin.Messages.SpawnVfx();
                }
            }

            if (toAdd > -1) {
                this.Plugin.Config.BannedTerritories.Add((uint) toAdd);
                if (this.Plugin.ClientState.TerritoryType == toAdd) {
                    this.Plugin.Framework.RunOnFrameworkThread(() => {
                        this.Plugin.Messages.RemoveVfx();
                        this.Plugin.Messages.Clear();
                    });
                }
            }

            if (toRemove > -1 || toAdd > -1) {
                this.Plugin.SaveConfig();
                this.FilterTerritories(this._filter);
            }
        }

        ImGui.EndChild();
    }

    private void DrawTerritory(uint rowId, string name, ref long toAdd) {
        if (this.Plugin.Config.BannedTerritories.Contains(rowId)) {
            return;
        }

        if (ImGui.Selectable($"{name}##{rowId}")) {
            toAdd = rowId;
        }
    }

    private void DrawBannedTerritory(uint terrId, string name, ref long toRemove) {
        if (ImGui.Selectable($"{name}##{terrId}", true)) {
            toRemove = terrId;
        }
    }

    private void DrawWriter(ref bool anyChanged, ref bool vfx) {
        if (ImGui.Button("Refresh packs")) {
            Pack.UpdatePacks();
        }

        var glyph = this.Plugin.Config.DefaultGlyph + 1;
        if (ImGui.InputInt("Default glyph", ref glyph)) {
            this.Plugin.Config.DefaultGlyph = Math.Min(Messages.VfxPaths.Length - 1, Math.Max(0, glyph - 1));
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
        ImGuiHelper.HelpIcon("Hold Ctrl to enable delete button.");

        ImGui.TextUnformatted("This will delete all your messages and votes.");
    }
}
