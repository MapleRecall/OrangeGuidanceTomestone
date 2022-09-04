using System.Numerics;
using ImGuiNET;
using OrangeGuidanceTomestone.Ui.MainWindowTabs;

namespace OrangeGuidanceTomestone.Ui;

internal class MainWindow {
    private Plugin Plugin { get; }
    private List<ITab> Tabs { get; }

    internal bool Visible;

    internal MainWindow(Plugin plugin) {
        this.Plugin = plugin;
        this.Tabs = new List<ITab> {
            new Write(this.Plugin),
            new MessageList(this.Plugin),
        };
    }

    internal void Draw() {
        if (!this.Visible) {
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(475, 300), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(this.Plugin.Name, ref this.Visible)) {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##ogt-main-tabs")) {
            foreach (var tab in this.Tabs) {
                if (!ImGui.BeginTabItem(tab.Name)) {
                    continue;
                }

                tab.Draw();

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }
}
