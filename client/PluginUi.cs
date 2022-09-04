using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using ImGuiNET;
using Newtonsoft.Json;

namespace OrangeGuidanceTomestone;

public class PluginUi : IDisposable {
    private Plugin Plugin { get; }

    internal bool WriterVisible;
    internal bool ViewerVisible;

    private int _pack;

    private int _part1 = -1;
    private (int, int) _word1 = (-1, -1);

    private int _part2 = -1;
    private (int, int) _word2 = (-1, -1);

    private int _conj = -1;

    internal PluginUi(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.Interface.UiBuilder.Draw += this.Draw;
    }

    public void Dispose() {
        this.Plugin.Interface.UiBuilder.Draw -= this.Draw;
    }

    private void Draw() {
        this.DrawWriter();
        this.DrawViewerButton();
        this.DrawViewer();
    }

    private void DrawWriter() {
        if (!this.WriterVisible) {
            return;
        }

        if (!ImGui.Begin(this.Plugin.Name, ref this.WriterVisible)) {
            ImGui.End();
            return;
        }

        var packPrev = Pack.All.Value[this._pack].Name;
        if (ImGui.BeginCombo("Pack", packPrev)) {
            for (var i = 0; i < Pack.All.Value.Length; i++) {
                var selPack = Pack.All.Value[i];
                if (!ImGui.Selectable(selPack.Name)) {
                    continue;
                }

                this._pack = i;

                this._part1 = -1;
                this._word1 = (-1, -1);
                this._conj = -1;
                this._part2 = -1;
                this._word2 = (-1, -1);
            }

            ImGui.EndCombo();
        }

        const string placeholder = "****";

        void DrawPicker(string id, IReadOnlyList<string> items, ref int x) {
            var preview = x == -1 ? "" : items[x].Replace("{0}", placeholder);
            if (!ImGui.BeginCombo(id, preview)) {
                return;
            }

            if (ImGui.Selectable("<none>")) {
                x = -1;
            }

            for (var i = 0; i < items.Count; i++) {
                var template = items[i].Replace("{0}", placeholder);
                if (ImGui.Selectable(template, i == x)) {
                    x = i;
                }
            }

            ImGui.EndCombo();
        }

        void DrawWordPicker(string id, IReadOnlyList<WordList> words, ref (int, int) x) {
            var preview = x == (-1, -1) ? "" : words[x.Item1].Words[x.Item2];
            if (!ImGui.BeginCombo(id, preview)) {
                return;
            }

            for (var listIdx = 0; listIdx < words.Count; listIdx++) {
                var list = words[listIdx];
                if (!ImGui.BeginMenu(list.Name)) {
                    continue;
                }

                for (var wordIdx = 0; wordIdx < list.Words.Length; wordIdx++) {
                    if (ImGui.MenuItem(list.Words[wordIdx])) {
                        x = (listIdx, wordIdx);
                    }
                }

                ImGui.EndMenu();
            }

            ImGui.EndCombo();
        }

        var pack = Pack.All.Value[this._pack];

        var actualText = string.Empty;
        if (this._part1 == -1) {
            ImGui.TextUnformatted(placeholder);
        } else {
            var preview = new StringBuilder();

            var template1 = pack.Templates[this._part1];
            var word1 = this._word1 == (-1, -1) ? placeholder : pack.Words[this._word1.Item1].Words[this._word1.Item2];
            preview.Append(string.Format(template1, word1));

            if (this._conj != -1) {
                var conj = pack.Conjunctions[this._conj];
                if (conj.Length != 1 || !char.IsPunctuation(conj[0])) {
                    preview.Append('\n');
                }

                preview.Append(conj);
                preview.Append(' ');

                if (this._part2 != -1) {
                    var template2 = pack.Templates[this._part2];
                    var word2 = this._word2 == (-1, -1) ? placeholder : pack.Words[this._word2.Item1].Words[this._word2.Item2];
                    preview.Append(string.Format(template2, word2));
                }
            }

            actualText = preview.ToString();
            ImGui.TextUnformatted(actualText);
        }

        ImGui.Separator();

        DrawPicker("Template##part-1", pack.Templates, ref this._part1);
        if (this._part1 > -1 && pack.Templates[this._part1].Contains("{0}")) {
            DrawWordPicker("Word##word-1", pack.Words, ref this._word1);
        }

        DrawPicker("Conjunction##conj", pack.Conjunctions, ref this._conj);

        if (this._conj != -1) {
            DrawPicker("Template##part-2", pack.Templates, ref this._part2);
            if (this._part2 > -1 && pack.Templates[this._part2].Contains("{0}")) {
                DrawWordPicker("Word##word-2", pack.Words, ref this._word2);
            }
        }

        this.ClearIfNecessary();

        var valid = this.ValidSetup();
        if (!valid) {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Write") && valid && this.Plugin.ClientState.LocalPlayer is { } player) {
            var req = new MessageRequest {
                Territory = this.Plugin.ClientState.TerritoryType,
                X = player.Position.X,
                Y = player.Position.Y,
                Z = player.Position.Z,
                PackId = pack.Id,
                Template1 = this._part1,
                Word1List = this._word1.Item1 == -1 ? null : this._word1.Item1,
                Word1Word = this._word1.Item2 == -1 ? null : this._word1.Item2,
                Conjunction = this._conj == -1 ? null : this._conj,
                Template2 = this._part2 == -1 ? null : this._part2,
                Word2List = this._word2.Item1 == -1 ? null : this._word2.Item1,
                Word2Word = this._word2.Item2 == -1 ? null : this._word2.Item2,
            };

            var json = JsonConvert.SerializeObject(req);
            Task.Run(async () => {
                var content = new StringContent(json) {
                    Headers = {
                        ContentType = new MediaTypeHeaderValue("application/json"),
                    },
                };

                content.Headers.Add("X-Api-Key", this.Plugin.Config.ApiKey);

                var resp = await new HttpClient().PostAsync("https://tryfingerbuthole.anna.lgbt/messages", content);
                var id = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode) {
                    var newMsg = new Message {
                        Id = Guid.Parse(id),
                        X = player.Position.X,
                        Y = player.Position.Y,
                        Z = player.Position.Z,
                        Text = actualText,
                        NegativeVotes = 0,
                        PositiveVotes = 0,
                    };

                    this.Plugin.Messages.Add(newMsg);
                    this.ResetWriter();
                    this.WriterVisible = false;
                }
            });
        }

        if (!valid) {
            ImGui.EndDisabled();
        }

        if (this.Plugin.ClientState.LocalPlayer is { } player2) {
            foreach (var msg in this.Plugin.Messages.Nearby()) {
                ImGui.TextUnformatted($"{msg.Text}: {Vector3.Distance(msg.Position, player2.Position):N2}");
            }
        }

        ImGui.End();
    }

    private void ResetWriter() {
        this._part1 = this._part2 = this._conj = -1;
        this._word1 = (-1, -1);
        this._word2 = (-1, -1);
    }

    private void ClearIfNecessary() {
        if (this._pack == -1) {
            this._part1 = -1;
        }

        var pack = Pack.All.Value[this._pack];

        if (this._part1 == -1 || !pack.Templates[this._part1].Contains("{0}")) {
            this._word1 = (-1, -1);
        }

        if (this._conj == -1) {
            this._part2 = -1;
        }

        if (this._part2 == -1 || !pack.Templates[this._part2].Contains("{0}")) {
            this._word2 = (-1, -1);
        }
    }

    private bool ValidSetup() {
        if (this._pack == -1 || this._part1 == -1) {
            return false;
        }

        var pack = Pack.All.Value[this._pack];
        var template1 = pack.Templates[this._part1];
        var temp1Variable = template1.Contains("{0}");

        switch (temp1Variable) {
            case true when this._word1 == (-1, -1):
            case false when this._word1 != (-1, -1):
                return false;
        }

        if (this._conj == -1 && (this._part2 != -1 || this._word2 != (-1, -1))) {
            return false;
        }

        if (this._conj != -1) {
            if (this._part2 == -1) {
                return false;
            }

            var template2 = pack.Templates[this._part2];
            var temp2Variable = template2.Contains("{0}");

            switch (temp2Variable) {
                case true when this._word2 == (-1, -1):
                case false when this._word2 != (-1, -1):
                    return false;
            }
        }

        return true;
    }

    private void DrawViewerButton() {
        if (this.ViewerVisible) {
            return;
        }

        var nearby = this.Plugin.Messages.Nearby().ToList();
        if (nearby.Count == 0) {
            return;
        }

        ImGui.SetNextWindowBgAlpha(0.5f);
        if (!ImGui.Begin("##ogt-viewer-button", ImGuiWindowFlags.NoTitleBar)) {
            ImGui.End();
            return;
        }

        var label = "View message";
        if (nearby.Count > 1) {
            label += "s";
        }

        if (ImGui.Button(label)) {
            this.ViewerVisible = true;
        }

        ImGui.End();
    }

    private int _viewerIdx;

    private void DrawViewer() {
        if (!this.ViewerVisible) {
            return;
        }

        if (!ImGui.Begin("Messages", ref this.ViewerVisible)) {
            ImGui.End();
            return;
        }

        if (ImGui.IsWindowAppearing()) {
            this._viewerIdx = 0;
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
            if (this._viewerIdx == 0) {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button("<")) {
                this._viewerIdx -= 1;
            }

            if (this._viewerIdx == 0) {
                ImGui.EndDisabled();
            }
        }

        if (ImGui.TableSetColumnIndex(1) && this._viewerIdx > -1 && this._viewerIdx < nearby.Count) {
            var message = nearby[this._viewerIdx];
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

            if (this._viewerIdx == nearby.Count - 1) {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button(">")) {
                this._viewerIdx += 1;
            }

            if (this._viewerIdx == nearby.Count - 1) {
                ImGui.EndDisabled();
            }
        }

        ImGui.EndTable();

        End:
        ImGui.End();
    }
}
