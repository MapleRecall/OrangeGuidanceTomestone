using Dalamud.Game.Command;
using Dalamud.Utility;
using Lumina.Excel.GeneratedSheets;

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
            case "ban": {
                var name = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(this.Plugin.ClientState.TerritoryType)
                    ?.PlaceName
                    .Value
                    ?.Name
                    ?.ToDalamudString()
                    .TextValue;

                if (this.Plugin.Config.BannedTerritories.Contains(this.Plugin.ClientState.TerritoryType)) {
                    this.Plugin.ChatGui.Print($"{name} is already on the ban list.");
                    return;
                }

                this.Plugin.Config.BannedTerritories.Add(this.Plugin.ClientState.TerritoryType);
                this.Plugin.SaveConfig();
                this.Plugin.ChatGui.Print($"Added {name} to the ban list.");

                this.Plugin.Messages.RemoveVfx();
                this.Plugin.Messages.Clear();
                break;
            }
            case "unban": {
                var name = this.Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(this.Plugin.ClientState.TerritoryType)
                    ?.PlaceName
                    .Value
                    ?.Name
                    ?.ToDalamudString()
                    .TextValue;

                if (!this.Plugin.Config.BannedTerritories.Contains(this.Plugin.ClientState.TerritoryType)) {
                    this.Plugin.ChatGui.Print($"{name} is not on the ban list.");
                    return;
                }

                this.Plugin.Config.BannedTerritories.Remove(this.Plugin.ClientState.TerritoryType);
                this.Plugin.SaveConfig();
                this.Plugin.ChatGui.Print($"Removed {name} from the ban list.");

                this.Plugin.Messages.SpawnVfx();
                break;
            }
            case "refresh":
                this.Plugin.Messages.SpawnVfx();
                break;
            default:
                this.Plugin.Ui.MainWindow.Visible ^= true;
                break;
        }
    }
}
