using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OrangeGuidanceTomestone.Helpers;

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

        var flags = ImGuiWindowFlags.NoBringToFrontOnFocus;
        flags |= this.Plugin.Config.HideTitlebar ? ImGuiWindowFlags.NoTitleBar : ImGuiWindowFlags.None;
        flags |= this.Plugin.Config.LockViewer ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None;
        flags |= this.Plugin.Config.ClickThroughViewer ? ImGuiWindowFlags.NoInputs : ImGuiWindowFlags.None;
        ImGui.SetNextWindowSize(new Vector2(350, 175), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(this.Plugin.Config.ViewerOpacity / 100.0f);
        if (!ImGui.Begin("Messages", ref this.Visible, flags)) {
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
            if (this.Plugin.Config.AutoViewerClose) {
                this.Visible = false;
            } else {
                ImGui.TextUnformatted("No nearby messages");
            }

            goto End;
        }

        if (this._idx >= nearby.Count) {
            this._idx = Math.Max(0, nearby.Count - 1);
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
            var size = ImGui.CalcTextSize(message.Text, ImGui.GetContentRegionAvail().X).Y;
            size += ImGui.GetStyle().ItemSpacing.Y * 2;
            size += ImGui.CalcTextSize("A").Y;
            size += ImGuiHelpers.GetButtonSize("A").Y;
            var height = ImGui.GetContentRegionAvail().Y;
            ImGui.Dummy(new Vector2(1, height / 2 - size / 2 - ImGui.GetStyle().ItemSpacing.Y));

            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(message.Text);
            ImGui.PopTextWrapPos();

            var appraisals = Math.Max(0, message.PositiveVotes - message.NegativeVotes);
            ImGui.TextUnformatted($"Appraisals: {appraisals:N0}");

            void Vote(int way) {
                Task.Run(async () => {
                    var resp = await ServerHelper.SendRequest(
                        this.Plugin.Config.ApiKey,
                        HttpMethod.Patch,
                        $"/messages/{message.Id}/votes",
                        "application/json",
                        new StringContent(way.ToString())
                    );

                    if (resp.IsSuccessStatusCode) {
                        var oldWay = message.UserVote;
                        switch (oldWay) {
                            case 1:
                                message.PositiveVotes -= 1;
                                break;
                            case -1:
                                message.NegativeVotes -= 1;
                                break;
                        }

                        switch (way) {
                            case 1:
                                message.PositiveVotes += 1;
                                break;
                            case -1:
                                message.NegativeVotes += 1;
                                break;
                        }

                        message.UserVote = way;
                    }
                });
            }

            var vote = message.UserVote;
            if (vote == 1) {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Like")) {
                Vote(1);
            }

            if (vote == 1) {
                ImGui.EndDisabled();
            }

            ImGui.SameLine();

            if (vote == -1) {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("Dislike")) {
                Vote(-1);
            }

            if (vote == -1) {
                ImGui.EndDisabled();
            }
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
