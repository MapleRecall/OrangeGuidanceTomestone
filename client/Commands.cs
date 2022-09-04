using Dalamud.Game.Command;

namespace OrangeGuidanceTomestone; 

internal class Commands : IDisposable {
    private Plugin Plugin { get; }

    internal Commands(Plugin plugin) {
        this.Plugin = plugin;

        this.Plugin.CommandManager.AddHandler("/ogt", new CommandInfo(this.OnCommand));
    }

    public void Dispose() {
        this.Plugin.CommandManager.RemoveHandler("/ogt");
    }
    
    private void OnCommand(string command, string arguments) {
        this.Plugin.Ui.MainWindow.Visible ^= true;
    }
}
