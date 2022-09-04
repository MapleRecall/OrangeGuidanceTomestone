using Dalamud.Game.Command;

namespace OrangeGuidanceTomestone;

internal class Commands : IDisposable {
    private Plugin Plugin { get; }

    internal Commands(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.CommandManager.AddHandler("/ogt", new CommandInfo(this.OnCommand) {
            HelpMessage = "Toggle UI",
        });
    }

    public void Dispose() {
        this.Plugin.CommandManager.RemoveHandler("/ogt");
    }

    private void OnCommand(string command, string arguments) {
        switch (arguments) {
            case "ban":
                this.Plugin.Config.BannedTerritories.Add(this.Plugin.ClientState.TerritoryType);
                this.Plugin.SaveConfig();
                this.Plugin.Messages.SpawnVfx();
                break;
            case "unban":
                this.Plugin.Config.BannedTerritories.Remove(this.Plugin.ClientState.TerritoryType);
                this.Plugin.SaveConfig();
                this.Plugin.Messages.SpawnVfx();
                break;
            case "refresh":
                this.Plugin.Messages.SpawnVfx();
                break;
            default:
                this.Plugin.Ui.MainWindow.Visible ^= true;
                break;
        }
    }
}
