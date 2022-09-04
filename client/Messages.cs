using System.Numerics;
using Dalamud.Game;
using Newtonsoft.Json;

namespace OrangeGuidanceTomestone;

internal class Messages : IDisposable {
    private const string VfxPath = "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1d_o.avfx";

    private Plugin Plugin { get; }

    private Queue<Message> SpawnQueue { get; } = new();

    internal Messages(Plugin plugin) {
        this.Plugin = plugin;

        this.SpawnVfx();

        this.Plugin.Framework.Update += this.HandleSpawnQueue;
        this.Plugin.ClientState.Login += this.SpawnVfx;
        this.Plugin.ClientState.Logout += this.RemoveVfx;
        this.Plugin.ClientState.TerritoryChanged += this.SpawnVfx;
    }

    public void Dispose() {
        this.Plugin.ClientState.TerritoryChanged -= this.SpawnVfx;
        this.Plugin.ClientState.Logout -= this.RemoveVfx;
        this.Plugin.ClientState.Login -= this.SpawnVfx;
        this.Plugin.Framework.Update -= this.HandleSpawnQueue;

        this.RemoveVfx(null, null);
    }

    private unsafe void HandleSpawnQueue(Framework framework) {
        if (!this.SpawnQueue.TryDequeue(out var message)) {
            return;
        }

        this.Plugin.Vfx.SpawnStatic(VfxPath, new Vector3(message.X, message.Y, message.Z));
    }

    private void SpawnVfx(object? sender, EventArgs e) {
        this.SpawnVfx();
    }

    private void SpawnVfx(object? sender, ushort e) {
        this.SpawnVfx();
    }

    private void SpawnVfx() {
        var territory = this.Plugin.ClientState.TerritoryType;
        if (territory == 0) {
            return;
        }

        this.RemoveVfx(null, null);

        Task.Run(async () => {
            var resp = await new HttpClient().GetAsync($"https://tryfingerbuthole.anna.lgbt/messages/{territory}");
            var json = await resp.Content.ReadAsStringAsync();
            var messages = JsonConvert.DeserializeObject<Message[]>(json)!;
            foreach (var message in messages) {
                this.SpawnQueue.Enqueue(message);
            }
        });
    }

    private void RemoveVfx(object? sender, EventArgs? e) {
        this.Plugin.Vfx.RemoveAll();
    }
}
