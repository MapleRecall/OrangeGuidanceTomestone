using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class MessageList : ITab {
    public string Name => "Your messages";
    private Plugin Plugin { get; }
    private SortMode Sort { get; set; }

    private SemaphoreSlim MessagesMutex { get; } = new(1, 1);
    private List<MessageWithTerritory> Messages { get; } = new();

    internal MessageList(Plugin plugin) {
        this.Plugin = plugin;
    }

    public void Dispose() {
    }

    public void Draw() {
        if (ImGui.Button("Refresh")) {
            this.Refresh();
        }

        ImGui.SameLine();

        if (ImGui.BeginCombo("Sort", $"{this.Sort}")) {
            foreach (var mode in Enum.GetValues<SortMode>()) {
                if (ImGui.Selectable($"{mode}", mode == this.Sort)) {
                    this.Sort = mode;
                }
            }

            ImGui.EndCombo();
        }

        this.MessagesMutex.Wait();

        ImGui.TextUnformatted($"Messages: {this.Messages.Count:N0} / {10 + this.Plugin.Ui.MainWindow.ExtraMessages:N0}");

        ImGui.Separator();

        if (ImGui.BeginChild("##messages-list")) {
            var messages = this.Messages;
            if (this.Sort != SortMode.Date) {
                messages = messages.ToList();
                messages.Sort((a, b) => {
                    return this.Sort switch {
                        SortMode.Date => 0,
                        SortMode.Appraisals => Math.Max(b.PositiveVotes - b.NegativeVotes, 0)
                            .CompareTo(Math.Max(a.PositiveVotes - a.NegativeVotes, 0)),
                        SortMode.Likes => b.PositiveVotes.CompareTo(a.PositiveVotes),
                        SortMode.Dislikes => b.NegativeVotes.CompareTo(a.NegativeVotes),
                        SortMode.Location => a.Territory.CompareTo(b.Territory),
                        _ => throw new ArgumentOutOfRangeException(),
                    };
                });
            }

            foreach (var message in messages) {
                var territory = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(message.Territory);
                var territoryName = territory?.PlaceName.Value?.Name?.ToDalamudString().TextValue ?? "???";

                ImGui.TextUnformatted(message.Text);
                ImGui.TreePush();
                ImGui.TextUnformatted($"Location: {territoryName}");
                ImGui.SameLine();

                if (ImGui.SmallButton($"Open map##{message.Id}") && territory != null) {
                    this.Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(
                        territory.RowId,
                        territory.Map.Row,
                        (int) (message.X * 1_000),
                        (int) (message.Z * 1_000)
                    ));
                }

                var appraisals = Math.Max(0, message.PositiveVotes - message.NegativeVotes);
                ImGui.TextUnformatted($"Appraisals: {appraisals:N0} ({message.PositiveVotes:N0} - {message.NegativeVotes:N0})");
                if (ImGui.Button($"Delete##{message.Id}")) {
                    this.Delete(message.Id);
                }

                ImGui.TreePop();

                ImGui.Separator();
            }
        }

        ImGui.EndChild();

        this.MessagesMutex.Release();
    }

    private void Refresh() {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Get,
                "/messages?v=2"
            );
            var json = await resp.Content.ReadAsStringAsync();
            var messages = JsonConvert.DeserializeObject<MyMessages>(json)!;
            await this.MessagesMutex.WaitAsync();
            this.Plugin.Ui.MainWindow.ExtraMessages = messages.Extra;
            this.Messages.Clear();
            this.Messages.AddRange(messages.Messages);
            this.MessagesMutex.Release();
        });
    }

    private void Delete(Guid id) {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Delete,
                $"/messages/{id}"
            );

            if (resp.IsSuccessStatusCode) {
                this.Refresh();
                this.Plugin.Vfx.RemoveStatic(id);
                this.Plugin.Messages.Remove(id);
            }
        });
    }

    private enum SortMode {
        Date,
        Appraisals,
        Likes,
        Dislikes,
        Location,
    }
}
