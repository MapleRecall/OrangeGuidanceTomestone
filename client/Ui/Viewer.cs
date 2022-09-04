using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace OrangeGuidanceTomestone.Ui;

internal class Viewer {
    private Plugin Plugin { get; }

    internal bool Visible;

    private int _idx;

    internal Viewer(Plugin plugin) {
        this.Plugin = plugin;
    }

    internal void Draw() {
        if (!this.Visible) {
            return;
        }

        if (!ImGui.Begin("Messages", ref this.Visible)) {
            ImGui.End();
            return;
        }

        if (ImGui.IsWindowAppearing()) {
            this._idx = 0;
        }

        var nearby = this.Plugin.Messages.Nearby()
            .OrderBy(msg => msg.Id)
            .ToList();
        if (nearby.Count == 0) {
            ImGui.TextUnformatted("No nearby messages");
            goto End;
        }

        if (!ImGui.BeginTable("##viewer-table", 3)) {
            goto End;
        }

        ImGui.TableSetupColumn("##prev-arrow", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("##content", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##next-arrow", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableNextRow();

        if (ImGui.TableSetColumnIndex(0)) {
            var height = ImGui.GetContentRegionAvail().Y;
            var buttonHeight = ImGuiHelpers.GetButtonSize("<").Y;
            ImGui.Dummy(new Vector2(1, height / 2 - buttonHeight / 2 - ImGui.GetStyle().ItemSpacing.Y));
            if (this._idx == 0) {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("<")) {
                this._idx -= 1;
            }

            if (this._idx == 0) {
                ImGui.EndDisabled();
            }
        }

        if (ImGui.TableSetColumnIndex(1) && this._idx > -1 && this._idx < nearby.Count) {
            var message = nearby[this._idx];
            var size = ImGui.CalcTextSize(message.Text, ImGui.GetContentRegionAvail().X);
            var height = ImGui.GetContentRegionAvail().Y;
            ImGui.Dummy(new Vector2(1, height / 2 - size.Y / 2 - ImGui.GetStyle().ItemSpacing.Y));

            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(message.Text);
            ImGui.PopTextWrapPos();
        }

        if (ImGui.TableSetColumnIndex(2)) {
            var height = ImGui.GetContentRegionAvail().Y;
            var buttonHeight = ImGuiHelpers.GetButtonSize(">").Y;
            ImGui.Dummy(new Vector2(1, height / 2 - buttonHeight / 2 - ImGui.GetStyle().ItemSpacing.Y));

            if (this._idx == nearby.Count - 1) {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button(">")) {
                this._idx += 1;
            }

            if (this._idx == nearby.Count - 1) {
                ImGui.EndDisabled();
            }
        }

        ImGui.EndTable();

        End:
        ImGui.End();
    }
}
