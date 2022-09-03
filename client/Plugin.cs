using Dalamud.IoC;
using Dalamud.Plugin;

namespace OrangeGuidanceTomestone;

public class Plugin : IDalamudPlugin {
    public string Name => "Orange Guidance Tomestone";

    [PluginService]
    internal DalamudPluginInterface Interface { get; init; }
    
    internal PluginUi Ui { get; }
    
    public Plugin() {
        this.Ui = new PluginUi(this);
    }
    
    public void Dispose() {
        this.Ui.Dispose();
    }

}
