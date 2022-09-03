using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace OrangeGuidanceTomestone;

public class Plugin : IDalamudPlugin {
    public string Name => "Orange Guidance Tomestone";

    [PluginService]
    internal DalamudPluginInterface Interface { get; init; }

    [PluginService]
    internal ClientState ClientState { get; init; }

    [PluginService]
    internal Framework Framework { get; init; }

    internal Configuration Config { get; }
    internal Vfx Vfx { get; }
    internal PluginUi Ui { get; }
    internal Messages Messages { get; }

    public Plugin() {
        this.Config = this.Interface!.GetPluginConfig() as Configuration ?? new Configuration();
        if (this.Config.ApiKey == string.Empty) {
            Task.Run(async () => {
                var resp = await new HttpClient().PostAsync("https://tryfingerbuthole.anna.lgbt/account", null);
                var key = await resp.Content.ReadAsStringAsync();
                this.Config.ApiKey = key;
                this.SaveConfig();
            });
        }

        this.Vfx = new Vfx();
        this.Ui = new PluginUi(this);
        this.Messages = new Messages(this);
    }

    public void Dispose() {
        this.Messages.Dispose();
        this.Ui.Dispose();
    }

    internal void SaveConfig() {
        this.Interface.SavePluginConfig(this.Config);
    }
}
