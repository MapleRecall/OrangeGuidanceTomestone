using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Pack {
    internal static SemaphoreSlim AllMutex { get; } = new(1, 1);
    internal static Pack[] All { get; set; } = Array.Empty<Pack>();

    public string Name { get; init; }
    public Guid Id { get; init; }
    public string[] Templates { get; init; }
    public string[] Conjunctions { get; init; }
    public List<WordList> Words { get; init; }

    internal static void UpdatePacks() {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(null, HttpMethod.Get, "/packs");
            var json = await resp.Content.ReadAsStringAsync();
            var packs = JsonConvert.DeserializeObject<Pack[]>(json)!;
            await AllMutex.WaitAsync();
            try {
                All = packs;
            } finally {
                AllMutex.Release();
            }
        });
    }
}

[Serializable]
public class WordList {
    public string Name { get; init; }
    public string[] Words { get; init; }
}
