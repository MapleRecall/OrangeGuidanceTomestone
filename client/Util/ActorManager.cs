using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace OrangeGuidanceTomestone.Util;

internal class ActorManager : IDisposable {
    private Plugin Plugin { get; }
    private uint? _idx;
    private readonly Queue<BaseActorAction> _tasks = [];

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

        if (!this._tasks.TryPeek(out var actorAction)) {
            return;
        }

        var objMan = ClientObjectManager.Instance();
        var success = false;

        if (actorAction.Tries < 10) {
            try {
                actorAction.Tries += 1;
                success = actorAction.Run(this, objMan);
            } catch (Exception ex) {
                Plugin.Log.Error(ex, "Error in actor action queue");
            }
        } else {
            Plugin.Log.Warning("too many retries, skipping");
            success = true;
        }


        if (success) {
            this._tasks.Dequeue();
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


    }

    internal void Despawn() {
        if (this._idx == null) {
            return;
        }

        this._tasks.Enqueue(new DisableAction());
        this._tasks.Enqueue(new DeleteAction());
    }

    private unsafe abstract class BaseActorAction {
        /// <summary>
        /// Run this action.
        /// </summary>
        /// <returns>true if the action is finished, false if it should be run again</returns>
        public abstract bool Run(ActorManager manager, ClientObjectManager* objMan);

        public int Tries { get; set; }

        protected bool TryGetBattleChara(
            ActorManager manager,
            ClientObjectManager* objMan,
            out BattleChara* chara
        ) {
            chara = null;

            if (manager._idx is not { } idx) {
                Plugin.Log.Warning("tried to get battlechara but idx was null");
                return false;
            }

            var obj = objMan->GetObjectByIndex((ushort) idx);
            if (obj == null) {
                Plugin.Log.Warning("tried to get battlechara but it was null");
                return false;
            }

            chara = (BattleChara*) obj;
            return true;
        }
    }

    private unsafe class SpawnAction(Message message) : BaseActorAction {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan) {
            if (manager._idx != null) {
                Plugin.Log.Warning("refusing to spawn a second actor");
                return true;
            }

            var idx = objMan->CreateBattleCharacter();
            if (idx == 0xFFFFFFFF) {
                Plugin.Log.Debug("actor could not be spawned");
                return true;
            }

            manager._idx = idx;

            var chara = (BattleChara*) objMan->GetObjectByIndex((ushort) idx);

            chara->ObjectKind = ObjectKind.BattleNpc;
            chara->Position = message.Position;
            chara->Rotation = message.Yaw;
            var drawData = &chara->DrawData;
            drawData->CustomizeData = new CustomizeData();

            chara->Alpha = 0.25f;
            chara->SetMode(CharacterModes.AnimLock, 0);
            chara->Timeline.BaseOverride = 4818;

            manager._tasks.Enqueue(new EnableAction());
            return true;
        }
    }

    private unsafe class EnableAction : BaseActorAction {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan) {
            if (!this.TryGetBattleChara(manager, objMan, out var chara)) {
                return true;
            }

            if (!chara->IsReadyToDraw()) {
                return false;
            }

            chara->EnableDraw();
            return true;
        }
    }

    private unsafe class DisableAction : BaseActorAction {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan) {
            if (!this.TryGetBattleChara(manager, objMan, out var chara)) {
                return true;
            }

            chara->DisableDraw();
            return true;
        }
    }

    private unsafe class DeleteAction : BaseActorAction {
        public override bool Run(ActorManager manager, ClientObjectManager* objMan) {
            if (manager._idx is not { } idx) {
                Plugin.Log.Warning("delete action but idx was null");
                return true;
            }

            if (objMan->GetObjectByIndex((ushort) idx) == null) {
                Plugin.Log.Warning("delete action but object at idx was null");
                return true;
            }

            objMan->DeleteObjectByIndex((ushort) idx, 0);
            return true;
        }
    }
}
