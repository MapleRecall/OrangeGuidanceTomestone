using ImGuiNET;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

internal class MessageList : ITab {
    public string Name => "Your messages";
    private Plugin Plugin { get; }

    private SemaphoreSlim MessagesMutex { get; } = new(1, 1);
    private List<MessageWithTerritory> Messages { get; } = new();

    internal MessageList(Plugin plugin) {
        this.Plugin = plugin;
    }

    public void Draw() {
        if (ImGui.Button("Refresh")) {
            this.Refresh();
        }

        this.MessagesMutex.Wait();

        foreach (var message in this.Messages) {
            ImGui.TextUnformatted(message.Text);
            ImGui.TreePush();
            ImGui.TextUnformatted($"Likes: {message.PositiveVotes}");
            ImGui.TextUnformatted($"Dislikes: {message.NegativeVotes}");
            if (ImGui.Button($"Delete##{message.Id}")) {
                this.Delete(message.Id);
            }

            ImGui.TreePop();

            ImGui.Separator();
        }

        this.MessagesMutex.Release();
    }

    private void Refresh() {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Get,
                "/messages"
            );
            var json = await resp.Content.ReadAsStringAsync();
            var messages = JsonConvert.DeserializeObject<MessageWithTerritory[]>(json)!;
            await this.MessagesMutex.WaitAsync();
            this.Messages.Clear();
            this.Messages.AddRange(messages);
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
}
