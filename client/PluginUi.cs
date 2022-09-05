using ImGuiNET;
using OrangeGuidanceTomestone.Ui;

namespace OrangeGuidanceTomestone;

public class PluginUi : IDisposable {
    private Plugin Plugin { get; }

    internal MainWindow MainWindow { get; }
    internal Viewer Viewer { get; }
    internal ViewerButton ViewerButton { get; }

    private List<(string, string)> Modals { get; } = new();

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
        this.DrawModals();
    }

    private void DrawModals() {
        var toRemove = -1;
        for (var i = 0; i < this.Modals.Count; i++) {
            var (id, text) = this.Modals[i];
            if (!ImGui.BeginPopupModal(id)) {
                continue;
            }

            ImGui.TextUnformatted(text);
            ImGui.Separator();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.Button("Close")) {
                ImGui.CloseCurrentPopup();
                toRemove = i;
            }

            ImGui.EndPopup();
        }

        if (toRemove > -1) {
            this.Modals.RemoveAt(toRemove);
        }
    }

    internal void AddModal(string text) {
        this.AddModal(Guid.NewGuid().ToString(), text);
    }

    internal void AddModal(string id, string text) {
        this.Modals.Add((id, text));
    }
}
