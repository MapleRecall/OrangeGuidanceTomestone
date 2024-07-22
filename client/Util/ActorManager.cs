using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace OrangeGuidanceTomestone.Util;

internal class ActorManager : IDisposable {
    private Plugin Plugin { get; }
    private uint? _idx;
    private readonly Queue<Mode> _tasks = [];

    private enum Mode {
        Enable,
        Disable,
        Delete,
    }

    internal ActorManager(Plugin plugin) {
        this.Plugin = plugin;
        this.Plugin.Framework.Update += this.OnFramework;
        this.Plugin.Ui.Viewer.View += this.OnView;
    }

    public void Dispose() {
        this.Plugin.Ui.Viewer.View -= this.OnView;
        this.Plugin.Framework.Update -= this.OnFramework;
    }

    private unsafe void OnFramework(IFramework framework) {
        if (this._idx is not { } idx) {
            return;
        }

        if (!this._tasks.TryPeek(out var mode)) {
            return;
        }

        var success = false;

        var objMan = ClientObjectManager.Instance();
        var obj = objMan->GetObjectByIndex((ushort) idx);
        if (obj == null) {
            Plugin.Log.Warning("actor by index was null");
            return;
        }

        switch (mode) {
            case Mode.Disable: {
                Plugin.Log.Debug("disabling actor");
                obj->DisableDraw();
                success = true;
                break;
            }
            case Mode.Enable: {
                if (!obj->IsReadyToDraw()) {
                    Plugin.Log.Debug("not ready to draw");
                    break;
                }

                Plugin.Log.Debug("drawing actor");
                obj->EnableDraw();
                success = true;
                break;
            }
            case Mode.Delete: {
                Plugin.Log.Debug("deleting actor");
                objMan->DeleteObjectByIndex((ushort) idx, 0);
                this._idx = null;
                success = true;
                break;
            }
        }

        if (success) {
            var res = this._tasks.Dequeue();
            Plugin.Log.Debug($"deq {Enum.GetName(res)}");
        }
    }

    private void OnView(Message? message) {
        var msg = message == null ? "null" : "not null";
        Plugin.Log.Debug($"OnView message is {msg}");
        this.Despawn();

        if (message != null) {
            this.Spawn(message);
        }
    }

    internal unsafe void Spawn(Message message) {
        if (this._idx != null) {
            Plugin.Log.Warning("refusing to spawn more than one actor");
            return;
        }

        Plugin.Log.Debug("spawning actor");

        var objMan = ClientObjectManager.Instance();
        var idx = objMan->CreateBattleCharacter();
        if (idx == 0xFFFFFFFF) {
            Plugin.Log.Debug("actor could not be spawned");
            return;
        }

        this._idx = idx;

        var chara = (BattleChara*) objMan->GetObjectByIndex((ushort) idx);

        chara->ObjectKind = ObjectKind.BattleNpc;
        chara->Position = message.Position;
        chara->Rotation = message.Yaw;
        var drawData = &chara->DrawData;
        drawData->CustomizeData = new CustomizeData();

        chara->Alpha = 0.25f;
        chara->SetMode(CharacterModes.AnimLock, 0);
        chara->Timeline.BaseOverride = 4818;

        this._tasks.Enqueue(Mode.Enable);
    }

    internal void Despawn() {
        if (this._idx == null) {
            return;
        }

        this._tasks.Enqueue(Mode.Disable);
        this._tasks.Enqueue(Mode.Delete);
    }
}
