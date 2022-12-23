using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Pack
{
    internal static SemaphoreSlim AllMutex { get; } = new(1, 1);
    internal static Pack[] All { get; set; } = Array.Empty<Pack>();

    public static Dictionary<string, string> TemplatesZH { get; set; } = new();
    public static Dictionary<string, string> ConjunctionsZH { get; set; } = new();
    public static Dictionary<string, string> DictionaryZH { get; set; } = new();

    public string Name { get; init; }
    public Guid Id { get; init; }
    public string[] Templates { get; init; }
    public string[] Conjunctions { get; init; }
    public List<WordList> Words { get; init; }

    internal static void UpdatePacks()
    {
        Task.Run(async () =>
        {
            var resp = await ServerHelper.SendRequest(null, HttpMethod.Get, "/packs");
            var json = await resp.Content.ReadAsStringAsync();

            var packs = JsonConvert.DeserializeObject<Pack[]>(json)!;

            // read local zh json
            string jsonZh;
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "OrangeGuidanceTomestone.Resources.zh.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                jsonZh = reader.ReadToEnd();
            }

            var packsZh = JsonConvert.DeserializeObject<Pack[]>(jsonZh)!;

            foreach (var packZh in packsZh)
            {
                var index = packs.ToList().FindIndex(x => x.Id == packZh.Id);
                if (index != -1)
                {
                    var pack = packs[index];

                    for (int i = 0; i < packZh.Templates.Length; i++)
                    {
                        TemplatesZH[pack.Templates[i]] = packZh.Templates[i];
                    }

                    for (int i = 0; i < packZh.Conjunctions.Length; i++)
                    {
                        ConjunctionsZH[pack.Conjunctions[i]] = packZh.Conjunctions[i];
                    }

                    for (int i = 0; i < packZh.Words.Count; i++)
                    {
                        for (int j = 0; j < packZh.Words[i].Words.Length; j++)
                        {
                            DictionaryZH[pack.Words[i].Words[j]] = packZh.Words[i].Words[j];
                        }
                    }

                    packs[index] = packZh;
                }
            }

            await AllMutex.WaitAsync();
            All = packs;
            AllMutex.Release();
        });
    }
}

[Serializable]
public class WordList {
    public string Name { get; init; }
    public string[] Words { get; init; }
}
