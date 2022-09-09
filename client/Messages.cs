using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone;

internal class Messages : IDisposable {
    internal static readonly string[] VfxPaths = {
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1a_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1b_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1c_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1d_o.avfx",
        "bg/ffxiv/fst_f1/common/vfx/eff/b0941trp1e_o.avfx",
        "bg/ex2/02_est_e3/common/vfx/eff/b0941trp1f_o.avfx",
    };


    private Plugin Plugin { get; }

    private SemaphoreSlim CurrentMutex { get; } = new(1, 1);
    private Dictionary<Guid, Message> Current { get; } = new();
    private Queue<Message> SpawnQueue { get; } = new();

    private HashSet<uint> Trials { get; } = new();
    private HashSet<uint> DeepDungeons { get; } = new();

    private bool CutsceneActive {
        get {
            var condition = this.Plugin.Condition;
            return condition[ConditionFlag.OccupiedInCutSceneEvent]
                   || condition[ConditionFlag.WatchingCutscene78];
        }
    }

    private bool GposeActive {
        get {
            var condition = this.Plugin.Condition;
            return condition[ConditionFlag.WatchingCutscene];
        }
    }

    private bool _inCutscene;
    private bool _inGpose;

    internal Messages(Plugin plugin) {
        this.Plugin = plugin;

        foreach (var cfc in this.Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()!) {
            // Trials, Raids, and Ultimate Raids
            if (cfc.ContentType.Row is 4 or 5 or 28) {
                // "Raids" - but we only want non-alliance raids
                if (cfc.ContentType.Row == 5 && cfc.ContentMemberType.Row == 4) {
                    continue;
                }

                this.Trials.Add(cfc.TerritoryType.Row);
            }

            if (cfc.ContentType.Row == 21) {
                this.DeepDungeons.Add(cfc.TerritoryType.Row);
            }
        }

        if (this.Plugin.Config.ApiKey != string.Empty) {
            this.SpawnVfx();
        }

        this.Plugin.Framework.Update += this.RemoveConditionally;
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
        this.Plugin.Framework.Update -= this.RemoveConditionally;

        this.RemoveVfx();
    }

    private void RemoveConditionally(Framework framework) {
        var nowCutscene = this.CutsceneActive;
        var cutsceneChanged = this._inCutscene != nowCutscene;
        if (this.Plugin.Config.DisableInCutscene && cutsceneChanged) {
            if (nowCutscene) {
                this.RemoveVfx();
            } else {
                this.SpawnVfx();
            }
        }

        var nowGpose = this.GposeActive;
        var gposeChanged = this._inGpose != nowGpose;
        if (this.Plugin.Config.DisableInGpose && this.GposeActive) {
            if (nowGpose) {
                this.RemoveVfx();
            } else {
                this.SpawnVfx();
            }
        }

        this._inCutscene = nowCutscene;
        this._inGpose = nowGpose;
    }

    private unsafe void HandleSpawnQueue(Framework framework) {
        if (!this.SpawnQueue.TryDequeue(out var message)) {
            return;
        }

        PluginLog.Debug($"spawning vfx for {message.Id}");
        var rotation = Quaternion.CreateFromYawPitchRoll(message.Yaw, 0, 0);
        var path = message.Glyph < 0 || message.Glyph >= VfxPaths.Length
            ? VfxPaths[0]
            : VfxPaths[message.Glyph];
        if (this.Plugin.Vfx.SpawnStatic(message.Id, path, message.Position, rotation) == null) {
            PluginLog.Debug("trying again");
            this.SpawnQueue.Enqueue(message);
        }
    }

    private void SpawnVfx(object? sender, EventArgs e) {
        this.SpawnVfx();
    }

    private void SpawnVfx(object? sender, ushort e) {
        this.SpawnVfx();
    }

    internal void SpawnVfx() {
        var territory = this.Plugin.ClientState.TerritoryType;
        if (territory == 0 || this.Plugin.Config.BannedTerritories.Contains(territory)) {
            return;
        }

        if (this.Plugin.Config.DisableTrials && this.Trials.Contains(territory)) {
            return;
        }

        if (this.Plugin.Config.DisableDeepDungeon && this.DeepDungeons.Contains(territory)) {
            return;
        }

        if (this.Plugin.Config.DisableInCutscene && this.CutsceneActive) {
            return;
        }

        if (this.Plugin.Config.DisableInGpose && this.GposeActive) {
            return;
        }

        this.RemoveVfx(null, null);

        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(
                this.Plugin.Config.ApiKey,
                HttpMethod.Get,
                $"/messages/{territory}"
            );
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
        this.RemoveVfx();
    }

    internal void RemoveVfx() {
        this.Plugin.Vfx.RemoveAll();
    }

    internal void Clear() {
        this.CurrentMutex.Wait();
        this.Current.Clear();
        this.CurrentMutex.Release();
    }

    internal IEnumerable<Message> Nearby() {
        if (this.Plugin.ClientState.LocalPlayer is not { } player) {
            return Array.Empty<Message>();
        }

        var position = player.Position;

        this.CurrentMutex.Wait();
        var nearby = this.Current
            .Values
            .Where(msg => Math.Abs(msg.Position.Y - position.Y) <= 1f)
            .Where(msg => Vector3.Distance(msg.Position, position) <= 2f)
            .ToList();
        this.CurrentMutex.Release();

        return nearby;
    }

    internal void Add(Message message) {
        this.CurrentMutex.Wait();
        this.Current[message.Id] = message;
        this.CurrentMutex.Release();
        this.SpawnQueue.Enqueue(message);
    }

    internal void Remove(Guid id) {
        this.CurrentMutex.Wait();
        this.Current.Remove(id);
        this.CurrentMutex.Release();
    }
}
