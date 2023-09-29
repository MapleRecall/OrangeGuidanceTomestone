using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OrangeGuidanceTomestone.MiniPenumbra;
using XivCommon;

namespace OrangeGuidanceTomestone;

public class Plugin : IDalamudPlugin {
    internal static string Name => "Orange Guidance Tomestone";

    [PluginService]
    internal static IPluginLog Log { get; private set; }

    [PluginService]
    internal DalamudPluginInterface Interface { get; init; }

    [PluginService]
    internal IChatGui ChatGui { get; init; }

    [PluginService]
    internal IClientState ClientState { get; init; }

    [PluginService]
    internal ICommandManager CommandManager { get; init; }

    [PluginService]
    internal ICondition Condition { get; init; }

    [PluginService]
    internal IDataManager DataManager { get; init; }

    [PluginService]
    internal IFramework Framework { get; init; }

    [PluginService]
    internal IGameGui GameGui { get; init; }

    [PluginService]
    internal IGameInteropProvider GameInteropProvider { get; init; }

    internal Configuration Config { get; }
    internal XivCommonBase Common { get; }
    internal Vfx Vfx { get; }
    internal PluginUi Ui { get; }
    internal Messages Messages { get; }
    internal VfxReplacer VfxReplacer { get; }
    internal Commands Commands { get; }
    internal Pinger Pinger { get; }

    internal string AvfxFilePath { get; }

    public Plugin() {
        this.AvfxFilePath = this.CopyAvfxFile();

        this.Config = this.Interface!.GetPluginConfig() as Configuration ?? new Configuration();
        this.Common = new XivCommonBase();
        this.Vfx = new Vfx(this);
        this.Messages = new Messages(this);
        this.Ui = new PluginUi(this);
        this.VfxReplacer = new VfxReplacer(this);
        this.Commands = new Commands(this);
        this.Pinger = new Pinger(this);

        if (this.Config.ApiKey == string.Empty) {
            this.GetApiKey();
        }
    }

    public void Dispose() {
        this.Pinger.Dispose();
        this.Commands.Dispose();
        this.VfxReplacer.Dispose();
        this.Ui.Dispose();
        this.Messages.Dispose();
        this.Vfx.Dispose();
        this.Common.Dispose();
    }

    internal void SaveConfig() {
        this.Interface.SavePluginConfig(this.Config);
    }

    private string CopyAvfxFile() {
        var configDir = this.Interface!.GetPluginConfigDirectory();
        Directory.CreateDirectory(configDir);
        for (var i = 0; i < Messages.VfxPaths.Length; i++) {
            var letter = (char) ('a' + i);
            var stream = Resourcer.Resource.AsStreamUnChecked($"OrangeGuidanceTomestone.vfx.b0941trp1{letter}_o.avfx");
            var path = Path.Join(configDir, $"sign_{letter}.avfx");
            stream.CopyTo(File.Create(path));
        }

        return configDir;
    }

    internal void GetApiKey() {
        Task.Run(async () => {
            var resp = await new HttpClient().PostAsync("https://tryfingerbuthole.anna.lgbt/account", null);
            var key = await resp.Content.ReadAsStringAsync();
            this.Config.ApiKey = key;
            this.SaveConfig();
            this.Messages.SpawnVfx();
        });
    }
}
