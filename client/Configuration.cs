using Dalamud.Configuration;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public string ApiKey { get; set; } = string.Empty;
    public HashSet<uint> BannedTerritories { get; set; } = new();
    public bool DisableTrials = true;
    public bool DisableDeepDungeon = true;
    public bool RemoveGlow = true;
    public bool AutoViewer;
    public bool AutoViewerClose = true;
    public bool HideTitlebar;
    public float ViewerOpacity = 100.0f;
    public int DefaultGlyph = 3;
}
