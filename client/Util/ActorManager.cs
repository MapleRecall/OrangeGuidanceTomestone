using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.GeneratedSheets;

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

        if (this._idx != null) {
            unsafe {
                var objMan = ClientObjectManager.Instance();
                new DisableAction().Run(this, objMan);
                new DeleteAction().Run(this, objMan);
            }
        }
    }

    private unsafe void OnFramework(IFramework framework) {
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

        if (this.Plugin.Config.ShowEmotes && message?.Emote != null) {
            this.Spawn(message);
        }
    }

    internal void Spawn(Message message) {
        this._tasks.Enqueue(new SpawnAction(message));
    }

    internal void Despawn() {
        if (this._idx == null) {
            return;
        }

        this._tasks.Enqueue(new DisableAction());
        this._tasks.Enqueue(new DeleteAction());
    }

    private abstract unsafe class BaseActorAction {
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

            if (message.Emote == null) {
                Plugin.Log.Warning("refusing to spawn an actor for a message without an emote");
                return true;
            }

            var idx = objMan->CreateBattleCharacter();
            if (idx == 0xFFFFFFFF) {
                Plugin.Log.Debug("actor could not be spawned");
                return true;
            }

            manager._idx = idx;
            var emote = message.Emote;
            var emoteRow = manager.GetValidEmote(emote.Id);

            var chara = (BattleChara*) objMan->GetObjectByIndex((ushort) idx);

            chara->ObjectKind = ObjectKind.BattleNpc;
            chara->TargetableStatus = 0;
            chara->Position = message.Position;
            chara->Rotation = message.Yaw;
            var drawData = &chara->DrawData;

            var maxLen = Math.Min(sizeof(CustomizeData), emote.Customise.Count);
            var rawCustomise = (byte*) &drawData->CustomizeData;
            for (var i = 0; i < maxLen; i++) {
                rawCustomise[i] = emote.Customise[i];
            }

            for (var i = 0; i < Math.Min(drawData->EquipmentModelIds.Length, emote.Equipment.Length); i++) {
                var equip = emote.Equipment[i];
                drawData->Equipment((DrawDataContainer.EquipmentSlot) i) = new EquipmentModelId {
                    Id = equip.Id,
                    Variant = equip.Variant,
                    Stain0 = equip.Stain0,
                    Stain1 = equip.Stain1,
                };
            }

            if (emoteRow is { DrawsWeapon: true }) {
                for (var i = 0; i < Math.Min(drawData->WeaponData.Length, emote.Weapon.Length); i++) {
                    var weapon = emote.Weapon[i];
                    drawData->Weapon((DrawDataContainer.WeaponSlot) i).ModelId = new FFXIVClientStructs.FFXIV.Client.Game.Character.WeaponModelId {
                        Id = weapon.ModelId.Id,
                        Type = weapon.ModelId.Kind,
                        Variant = weapon.ModelId.Variant,
                        Stain0 = weapon.ModelId.Stain0,
                        Stain1 = weapon.ModelId.Stain1,
                    };
                    drawData->Weapon((DrawDataContainer.WeaponSlot) i).Flags1 = weapon.Flags1;
                    drawData->Weapon((DrawDataContainer.WeaponSlot) i).Flags2 = weapon.Flags2;
                    drawData->Weapon((DrawDataContainer.WeaponSlot) i).State = weapon.State;
                }
            }

            drawData->IsHatHidden = emote.HatHidden;
            drawData->IsVisorToggled = emote.VisorToggled;
            drawData->IsWeaponHidden = emote.WeaponHidden;

            drawData->SetGlasses(0, (ushort) emote.Glasses);

            chara->Alpha = 0.25f;
            chara->SetMode(CharacterModes.AnimLock, 0);
            if (emoteRow != null) {
                chara->Timeline.BaseOverride = (ushort) emoteRow.ActionTimeline[0].Row;
            }

            manager._tasks.Enqueue(new EnableAction());
            return true;
        }
    }

    private Emote? GetValidEmote(uint rowId) {
        var emote = this.Plugin.DataManager.GetExcelSheet<Emote>()?.GetRow(rowId);
        if (emote == null) {
            return null;
        }

        return emote.TextCommand.Row == 0 ? null : emote;
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
            manager._idx = null;
            return true;
        }
    }
}
