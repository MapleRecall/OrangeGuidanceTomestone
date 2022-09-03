using Dalamud.IoC;
using Dalamud.Plugin;

namespace OrangeGuidanceTomestone;

public class Plugin : IDalamudPlugin {
    public string Name => "Orange Guidance Tomestone";

    [PluginService]
    internal DalamudPluginInterface Interface { get; init; }
    
    public Plugin() {
        
    }
    
    public void Dispose() {
        throw new NotImplementedException();
    }

}
