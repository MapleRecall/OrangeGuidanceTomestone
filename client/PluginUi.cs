using System.Text;
using ImGuiNET;
using YamlDotNet.Serialization.NamingConventions;

namespace OrangeGuidanceTomestone;

public class PluginUi : IDisposable {
    private Plugin Plugin { get; }

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
        if (!ImGui.Begin("Orange Guidance Tomestone")) {
            ImGui.End();
            return;
        }

        void DrawPicker(string id, IReadOnlyList<string> items, ref int x) {
            var preview = x == -1 ? "" : items[x].Replace("{0}", "****");
            if (!ImGui.BeginCombo(id, preview)) {
                return;
            }

            for (var i = 0; i < items.Count; i++) {
                var template = items[i].Replace("{0}", "****");
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

        var pack = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Deserialize<Pack>(Resourcer.Resource.AsString("elden-ring.yaml"));

        if (this._part1 == -1) {
            ImGui.TextUnformatted("****");
        } else {
            var preview = new StringBuilder();

            var template1 = pack.Templates[this._part1];
            var word1 = this._word1 == (-1, -1) ? "****" : pack.Words[this._word1.Item1].Words[this._word1.Item2];
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
                    var word2 = this._word2 == (-1, -1) ? "****" : pack.Words[this._word2.Item1].Words[this._word2.Item2];
                    preview.Append(string.Format(template2, word2));
                }
            }

            ImGui.TextUnformatted(preview.ToString());
        }

        ImGui.Separator();

        DrawPicker("Template##part-1", pack.Templates, ref this._part1);
        DrawWordPicker("Word##word-1", pack.Words, ref this._word1);
        DrawPicker("Conjugation##conj", pack.Conjunctions, ref this._conj);
        DrawPicker("Template##part-2", pack.Templates, ref this._part2);
        DrawWordPicker("Word##word-2", pack.Words, ref this._word2);


        ImGui.End();
    }
}
