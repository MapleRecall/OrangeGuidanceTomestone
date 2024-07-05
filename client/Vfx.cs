using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

namespace OrangeGuidanceTomestone;

internal unsafe class Vfx : IDisposable {
    private static readonly byte[] Pool = "Client.System.Scheduler.Instance.VfxObject\0"u8.ToArray();

    [Signature("E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08")]
    private delegate* unmanaged<byte*, byte*, VfxStruct*> _staticVfxCreate;

    [Signature("E8 ?? ?? ?? ?? 8B 4B 7C 85 C9")]
    private delegate* unmanaged<VfxStruct*, float, int, ulong> _staticVfxRun;

    [Signature("40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9")]
    private delegate* unmanaged<VfxStruct*, nint> _staticVfxRemove;

    private Plugin Plugin { get; }
    private Dictionary<Guid, nint> Spawned { get; } = [];
    private Queue<nint> RemoveQueue { get; } = [];
    private bool _disposed;

    internal Vfx(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.GameInteropProvider.InitializeFromAttributes(this);
        this.Plugin.Framework.Update += this.OnFrameworkUpdate;
    }

    public void Dispose() {
        if (this._disposed) {
            return;
        }

        this._disposed = true;
        this.RemoveAll();
    }

    private void OnFrameworkUpdate(IFramework framework) {
        if (this._disposed && this.RemoveQueue.Count == 0) {
            this.Plugin.Framework.Update -= this.OnFrameworkUpdate;
        }

        if (!this.RemoveQueue.TryDequeue(out var vfx)) {
            return;
        }

        if (!this.RemoveStatic((VfxStruct*) vfx)) {
            this.RemoveQueue.Enqueue(vfx);
        }
    }

    internal void RemoveAll() {
        foreach (var spawned in this.Spawned.Keys.ToArray()) {
            this.RemoveStatic(spawned);
        }
    }

    internal VfxStruct* SpawnStatic(Guid id, string path, Vector3 pos, Quaternion rotation) {
        VfxStruct* vfx;
        fixed (byte* p = Encoding.UTF8.GetBytes(path).NullTerminate()) {
            fixed (byte* pool = Pool) {
                vfx = this._staticVfxCreate(p, pool);
            }
        }

        if (vfx == null) {
            return null;
        }

        // update position
        vfx->Position = new Vector3(pos.X, pos.Y, pos.Z);
        // update rotation
        vfx->Rotation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);

        // update
        vfx->Flags |= 2;

        this._staticVfxRun(vfx, 0.0f, -1);

        this.Spawned[id] = (nint) vfx;

        return vfx;
    }

    internal bool RemoveStatic(VfxStruct* vfx) {
        var result = this._staticVfxRemove(vfx);
        var success = result != 0;
        if (!success) {
            this.RemoveQueue.Enqueue((nint) vfx);
        }

        return success;
    }

    internal void RemoveStatic(Guid id) {
        if (!this.Spawned.Remove(id, out var vfx)) {
            return;
        }

        this.RemoveStatic((VfxStruct*) vfx);
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct VfxStruct {
        [FieldOffset(0x38)]
        public byte Flags;

        [FieldOffset(0x50)]
        public Vector3 Position;

        [FieldOffset(0x60)]
        public Quaternion Rotation;

        [FieldOffset(0x70)]
        public Vector3 Scale;

        [FieldOffset(0x128)]
        public int ActorCaster;

        [FieldOffset(0x130)]
        public int ActorTarget;

        [FieldOffset(0x1B8)]
        public int StaticCaster;

        [FieldOffset(0x1C0)]
        public int StaticTarget;
    }
}
