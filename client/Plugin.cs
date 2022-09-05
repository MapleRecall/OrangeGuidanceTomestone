using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using OrangeGuidanceTomestone.MiniPenumbra;

namespace OrangeGuidanceTomestone;

public class Plugin : IDalamudPlugin {
    public string Name => "Orange Guidance Tomestone";

    [PluginService]
    internal DalamudPluginInterface Interface { get; init; }

    [PluginService]
    internal ChatGui ChatGui { get; init; }

    [PluginService]
    internal ClientState ClientState { get; init; }

    [PluginService]
    internal CommandManager CommandManager { get; init; }

    [PluginService]
    internal Condition Condition { get; init; }

    [PluginService]
    internal DataManager DataManager { get; init; }

    [PluginService]
    internal Framework Framework { get; init; }

    internal Configuration Config { get; }
    internal Vfx Vfx { get; }
    internal PluginUi Ui { get; }
    internal Messages Messages { get; }
    internal VfxReplacer VfxReplacer { get; }
    internal Commands Commands { get; }

    internal string AvfxFilePath { get; }

    public Plugin() {
        this.AvfxFilePath = this.CopyAvfxFile();

        this.Config = this.Interface!.GetPluginConfig() as Configuration ?? new Configuration();
        this.Vfx = new Vfx();
        this.Messages = new Messages(this);
        this.Ui = new PluginUi(this);
        this.VfxReplacer = new VfxReplacer(this);
        this.Commands = new Commands(this);

        if (this.Config.ApiKey == string.Empty) {
            this.GetApiKey();
        }
    }

    public void Dispose() {
        this.Commands.Dispose();
        this.VfxReplacer.Dispose();
        this.Ui.Dispose();
        this.Messages.Dispose();
        this.Vfx.Dispose();
    }

    internal void SaveConfig() {
        this.Interface.SavePluginConfig(this.Config);
    }

    private string CopyAvfxFile() {
        var configDir = this.Interface!.GetPluginConfigDirectory();
        Directory.CreateDirectory(configDir);
        var stream = Resourcer.Resource.AsStream("MiniPenumbra/b0941trp1d_o.avfx");
        var path = Path.Join(configDir, "sign.avfx");
        stream.CopyTo(File.Create(path));
        return path;
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
