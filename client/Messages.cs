using System.Numerics;
using Dalamud.Game;
using Newtonsoft.Json;

namespace OrangeGuidanceTomestone;

internal class Messages : IDisposable {
    private const string VfxPath = "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1d_o.avfx";

    private Plugin Plugin { get; }

    private SemaphoreSlim CurrentMutex { get; } = new(1, 1);
    private Dictionary<Guid, Message> Current { get; } = new();
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

        if (this.Plugin.Vfx.SpawnStatic(VfxPath, message.Position) == null) {
            this.SpawnQueue.Enqueue(message);
        }
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

            await this.CurrentMutex.WaitAsync();
            this.Current.Clear();

            foreach (var message in messages) {
                this.Current[message.Id] = message;
                this.SpawnQueue.Enqueue(message);
            }

            this.CurrentMutex.Release();
        });
    }

    private void RemoveVfx(object? sender, EventArgs? e) {
        this.Plugin.Vfx.RemoveAll();
    }

    internal IEnumerable<Message> Nearby() {
        if (this.Plugin.ClientState.LocalPlayer is not { } player) {
            return Array.Empty<Message>();
        }

        var position = player.Position;

        this.CurrentMutex.Wait();
        var nearby = this.Current
            .Values
            .Where(msg => Math.Abs(msg.Position.Y - position.Y) < 1f)
            .Where(msg => Vector3.Distance(msg.Position, position) < 5f)
            .ToList();
        this.CurrentMutex.Release();

        return nearby;
    }

    internal void Add(Message message) {
        this.Current[message.Id] = message;
        this.SpawnQueue.Enqueue(message);
    }
}