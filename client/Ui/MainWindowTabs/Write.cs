using System.Numerics;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Internal;
using ImGuiNET;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class Write : ITab {
    public string Name => "Write";

    private Plugin Plugin { get; }

    private int _pack;
    private int _part1 = -1;
    private (int, int) _word1 = (-1, -1);
    private int _conj = -1;
    private int _part2 = -1;
    private (int, int) _word2 = (-1, -1);
    private int _glyph;

    private List<IDalamudTextureWrap> GlyphImages { get; } = [];

    private void LoadSignImages() {
        for (var i = 0; i < Messages.VfxPaths.Length; i++) {
            var stream = Resourcer.Resource.AsStreamUnChecked($"OrangeGuidanceTomestone.img.sign_{i}.jpg");
            using var mem = new MemoryStream();
            stream.CopyTo(mem);
            var wrap = this.Plugin.Interface.UiBuilder.LoadImage(mem.ToArray());
            this.GlyphImages.Add(wrap);
        }
    }

    internal Write(Plugin plugin) {
        this.Plugin = plugin;
        this.LoadSignImages();

        this._glyph = this.Plugin.Config.DefaultGlyph;
        Pack.UpdatePacks();
    }

    public void Dispose() {
        foreach (var wrap in this.GlyphImages) {
            wrap.Dispose();
        }
    }

    public void Draw() {
        Pack.AllMutex.Wait();

        try {
            this.DrawInner();
        } finally {
            Pack.AllMutex.Release();
        }
    }

    private void DrawInner() {
        if (Pack.All.Length == 0) {
            ImGui.TextUnformatted("Please refresh the packs from the settings.");
            return;
        }

        if (this._pack < 0 || this._pack >= Pack.All.Length) {
            this._pack = 0;
        }

        var packPrev = Pack.All[this._pack].Name;
        if (ImGui.BeginCombo("Pack", packPrev)) {
            for (var i = 0; i < Pack.All.Length; i++) {
                var selPack = Pack.All[i];
                if (!ImGui.Selectable(selPack.Name)) {
                    continue;
                }

                this._pack = i;
                this.ResetWriter();
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

        var pack = Pack.All[this._pack];

        var lineHeight = ImGui.CalcTextSize("A").Y;
        var imageHeight = lineHeight * 4;

        var actualText = string.Empty;

        if (ImGui.BeginTable("##message-preview", 2)) {
            ImGui.TableSetupColumn("##image", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("##message", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            if (ImGui.TableSetColumnIndex(0)) {
                var glyphImage = this.GlyphImages[this._glyph];
                ImGui.Image(glyphImage.ImGuiHandle, new Vector2(imageHeight));
            }

            if (ImGui.TableSetColumnIndex(1) && this._part1 != -1) {
                var preview = new StringBuilder();

                var template1 = pack.Templates[this._part1];
                var word1 = this._word1 == (-1, -1) ? placeholder : pack.Words[this._word1.Item1].Words[this._word1.Item2];
                preview.Append(string.Format(template1, word1));

                if (this._conj != -1) {
                    var conj = pack.Conjunctions[this._conj];
                    var isPunc = conj.Length == 1 && char.IsPunctuation(conj[0]);
                    if (isPunc) {
                        preview.Append(conj);
                        preview.Append('\n');
                    } else {
                        preview.Append('\n');
                        preview.Append(conj);
                        preview.Append(' ');
                    }

                    if (this._part2 != -1) {
                        var template2 = pack.Templates[this._part2];
                        var word2 = this._word2 == (-1, -1) ? placeholder : pack.Words[this._word2.Item1].Words[this._word2.Item2];
                        preview.Append(string.Format(template2, word2));
                    }
                }

                actualText = preview.ToString();
                var actualSize = ImGui.CalcTextSize(actualText);
                ImGui.Dummy(new Vector2(1, imageHeight / 2 - actualSize.Y / 2 - ImGui.GetStyle().ItemSpacing.Y));
                ImGui.TextUnformatted(actualText);
            }

            ImGui.EndTable();
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

        if (ImGui.BeginCombo("Glyph", $"{this._glyph + 1}")) {
            var tooltipShown = false;

            for (var i = 0; i < Messages.VfxPaths.Length; i++) {
                if (ImGui.Selectable($"{i + 1}", this._glyph == i)) {
                    this._glyph = i;
                }

                if (tooltipShown || !ImGui.IsItemHovered()) {
                    continue;
                }

                ImGui.BeginTooltip();
                var image = this.GlyphImages[i];
                ImGui.Image(image.ImGuiHandle, new Vector2(imageHeight));
                ImGui.EndTooltip();
                tooltipShown = true;
            }

            ImGui.EndCombo();
        }

        this.ClearIfNecessary();

        var valid = this.ValidSetup();
        if (!valid) {
            ImGui.BeginDisabled();
        }

        var inAir = this.Plugin.Condition[ConditionFlag.Jumping]
                    || this.Plugin.Condition[ConditionFlag.Jumping61]
                    || this.Plugin.Condition[ConditionFlag.InFlight];
        if (ImGui.Button("Write") && valid && !inAir && this.Plugin.ClientState.LocalPlayer is { } player) {
            var req = new MessageRequest {
                Territory = this.Plugin.ClientState.TerritoryType,
                World = this.Plugin.ClientState.LocalPlayer?.CurrentWorld.Id ?? 0,
                Ward = this.Plugin.Common.Functions.Housing.Location?.Ward,
                Plot = this.Plugin.Common.Functions.Housing.Location?.CombinedPlot(),
                X = player.Position.X,
                Y = player.Position.Y,
                Z = player.Position.Z,
                Yaw = player.Rotation,
                PackId = pack.Id,
                Template1 = this._part1,
                Word1List = this._word1.Item1 == -1 ? null : this._word1.Item1,
                Word1Word = this._word1.Item2 == -1 ? null : this._word1.Item2,
                Conjunction = this._conj == -1 ? null : this._conj,
                Template2 = this._part2 == -1 ? null : this._part2,
                Word2List = this._word2.Item1 == -1 ? null : this._word2.Item1,
                Word2Word = this._word2.Item2 == -1 ? null : this._word2.Item2,
                Glyph = this._glyph,
            };

            var json = JsonConvert.SerializeObject(req);
            Task.Run(async () => {
                var resp = await ServerHelper.SendRequest(
                    this.Plugin.Config.ApiKey,
                    HttpMethod.Post,
                    "/messages",
                    "application/json",
                    new StringContent(json)
                );
                var content = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode) {
                    var newMsg = new Message {
                        Id = Guid.Parse(content),
                        X = player.Position.X,
                        Y = player.Position.Y,
                        Z = player.Position.Z,
                        Yaw = player.Rotation,
                        Text = actualText,
                        NegativeVotes = 0,
                        PositiveVotes = 0,
                        Glyph = this._glyph,
                    };

                    this.Plugin.Messages.Add(newMsg);
                    this.ResetWriter();
                    this.Plugin.Ui.MainWindow.Visible = false;
                } else {
                    var error = JsonConvert.DeserializeObject<ErrorMessage>(content);
                    this.Plugin.Ui.ShowModal($"Error writing message.\n\nMessage from server:\n{error?.Message}");
                }
            });
        }

        if (!valid) {
            ImGui.EndDisabled();
        }
    }

    private void ResetWriter() {
        this._part1 = this._part2 = this._conj = -1;
        this._word1 = (-1, -1);
        this._word2 = (-1, -1);
        this._glyph = this.Plugin.Config.DefaultGlyph;
    }

    private void ClearIfNecessary() {
        if (this._pack == -1) {
            this._part1 = -1;
        }

        var pack = Pack.All[this._pack];

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

        var pack = Pack.All[this._pack];
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
}
