using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Web;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using OrangeGuidanceTomestone.Helpers;
using WebTranslator.Baidu;

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

        var flags = ImGuiWindowFlags.NoFocusOnAppearing;
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

            if (ImGuiHelper.IconButton(FontAwesomeIcon.AngleLeft)) {
                this._idx -= 1;
            }

            if (this._idx == 0) {
                ImGui.EndDisabled();
            }
        }

        if (ImGui.TableSetColumnIndex(1) && this._idx > -1 && this._idx < nearby.Count) {
            var message = nearby[this._idx];

            if (string.IsNullOrWhiteSpace(message.TextZh) && !message.Translating)
            {
                message.Translating = true;
                Task.Run(() =>
                {
                    try
                    {
                        BaiduTranslate.appId = "20221223001505811";
                        BaiduTranslate.secretKey = "vym5TLDMKtsSJ3uysAG1";

                        var msg = message.Text;
                        PluginLog.Debug(message.Text);

                        var msgParts = msg.Split("\n");
                        var conjunction = string.Empty;
                        var conjunctionZh = string.Empty;

                        if (msgParts.Length > 1)
                        {
                            foreach (var pair in Pack.ConjunctionsZH.OrderByDescending(x => x.Key.Length))
                            {
                                var reg = new Regex($"(^|\\b)({Regex.Escape(pair.Key)})(\\b|$)", RegexOptions.IgnoreCase);
                                PluginLog.Verbose($"[Conjunctions] {reg}");

                                if (reg.IsMatch(msgParts[1]))
                                {
                                    conjunction = pair.Key;
                                    conjunctionZh = pair.Value;
                                    msgParts[1] = msgParts[1].Replace(conjunction, string.Empty);
                                    PluginLog.Debug($"[Conjunctions]");
                                    PluginLog.Debug($"[Conjunctions] {conjunction} >> {conjunctionZh}");
                                    PluginLog.Debug($"[Conjunctions] {msgParts[0]} : {msgParts[1]}");

                                    break;
                                }
                            }
                        }

                        foreach (var pair in Pack.TemplatesZH.OrderByDescending(x => x.Key.Length))
                        {
                            var reg = new Regex($"{Regex.Escape(pair.Key).Replace("\\{0}", "(?<content>.+)")}", RegexOptions.IgnoreCase);
                            PluginLog.Verbose($"[Templates] {reg}");

                            for (int i = 0; i < msgParts.Length; i++)
                            {
                                var match = reg.Match(msgParts[i]);
                                if (match.Success)
                                {
                                    PluginLog.Debug($"[Templates]");
                                    PluginLog.Debug($"[Templates] [FOO] {msgParts[i]}");
                                    msgParts[i] = string.Format(pair.Value, match.Groups["content"].Value);
                                    PluginLog.Debug($"[Templates] {pair.Key} >> {pair.Value}");
                                    PluginLog.Debug($"[Templates] [BAR] {msgParts[i]}");
                                }
                            }
                        };

                        if (msgParts.Length > 1)
                        {
                            msgParts[1] = conjunctionZh + msgParts[1];
                        }

                        msg = string.Join("\n", msgParts);

                        PluginLog.Debug($"[Dictionary]");
                        PluginLog.Debug($"[Dictionary] [FOO] {msg}");
                        foreach (var pair in Pack.DictionaryZH.OrderByDescending(x => x.Key.Length))
                        {
                            var reg = new Regex($"(^|\\b)({Regex.Escape(pair.Key)})(\\b|$)", RegexOptions.IgnoreCase);
                            PluginLog.Verbose($"[Dictionary] {reg}");
                            msg = reg.Replace(msg, pair.Value);
                        };

                        PluginLog.Debug($"[Dictionary] [BAR] {msg}");

                        message.TextZh = msg;

                        var enCount = new Regex("[a-z]", RegexOptions.IgnoreCase).Matches(msg).Count;
                        PluginLog.Debug($"[EN Count] {enCount}");

                        if (enCount > 1)
                        {
                            msg = msg.ToLower().Replace("\n", " [%0A] ");
                            PluginLog.Debug($"[Translate] {msg}");
                            msg = BaiduTranslate.Baidu_Translate(baidu_lan.en.ToString(), baidu_lan.zh.ToString(), msg).Result.Replace("[%0A]", "\n");
                            PluginLog.Debug($"[Translate] end");
                        }

                        message.TextZh = msg.Replace(" ", "");

                        PluginLog.Debug(message.TextZh);
                    }
                    finally
                    {
                        message.Translating = false;
                    }

                });

            }

            var text = string.IsNullOrWhiteSpace(message.TextZh) ? message.Text : $"{message.TextZh}\n\n{message.Text}\n\n";
            var size = ImGui.CalcTextSize(text, ImGui.GetContentRegionAvail().X).Y;
            size += ImGui.GetStyle().ItemSpacing.Y * 2;
            size += ImGui.CalcTextSize("A").Y;
            size += ImGuiHelpers.GetButtonSize("A").Y;
            var height = ImGui.GetContentRegionAvail().Y;
            ImGui.Dummy(new Vector2(1, height / 2 - size / 2 - ImGui.GetStyle().ItemSpacing.Y));

            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(text);
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

            if (ImGuiHelper.IconButton(FontAwesomeIcon.AngleRight)) {
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
