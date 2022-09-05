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
}
