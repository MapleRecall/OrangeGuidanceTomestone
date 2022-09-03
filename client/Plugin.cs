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

    internal Configuration Config { get; }
    internal PluginUi Ui { get; }

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

        this.Ui = new PluginUi(this);
    }

    public void Dispose() {
        this.Ui.Dispose();
    }

    internal void SaveConfig() {
        this.Interface.SavePluginConfig(this.Config);
    }
}
