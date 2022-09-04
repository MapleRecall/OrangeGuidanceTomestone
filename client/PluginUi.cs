using OrangeGuidanceTomestone.Ui;

namespace OrangeGuidanceTomestone;

public class PluginUi : IDisposable {
    private Plugin Plugin { get; }

    internal MainWindow MainWindow { get; }
    internal Viewer Viewer { get; }
    internal ViewerButton ViewerButton { get; }

    internal PluginUi(Plugin plugin) {
        this.Plugin = plugin;
        this.MainWindow = new MainWindow(this.Plugin);
        this.Viewer = new Viewer(this.Plugin);
        this.ViewerButton = new ViewerButton(this.Plugin);

        this.Plugin.Interface.UiBuilder.Draw += this.Draw;
    }

    public void Dispose() {
        this.Plugin.Interface.UiBuilder.Draw -= this.Draw;
    }

    private void Draw() {
        this.MainWindow.Draw();
        this.ViewerButton.Draw();
        this.Viewer.Draw();
    }
}
