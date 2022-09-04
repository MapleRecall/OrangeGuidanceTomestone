using Dalamud.Configuration;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public string ApiKey { get; set; } = string.Empty;
    public HashSet<uint> BannedTerritories { get; set; } = new();
}
