using Dalamud.Logging;
using YamlDotNet.Serialization.NamingConventions;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Pack {
    internal static Lazy<Pack[]> All { get; } = new(() => {
        var des = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return new[] {
                // "ffxiv",
                "elden-ring",
                // "dark-souls",
            }
            .Select(name => {
                try {
                    return des.Deserialize<Pack>(Resourcer.Resource.AsStringUnChecked($"OrangeGuidanceTomestone.packs.{name}.yaml"));
                } catch (Exception ex) {
                    PluginLog.LogError(ex, name);
                    return null;
                }
            })
            .Where(pack => pack != null)
            .ToArray()!;
    });

    public string Name { get; init; }
    public Guid Id { get; init; }
    public string[] Templates { get; init; }
    public string[] Conjunctions { get; init; }
    public List<WordList> Words { get; init; }
}

[Serializable]
public class WordList {
    public string Name { get; init; }
    public string[] Words { get; init; }
}
