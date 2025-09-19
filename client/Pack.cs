using System.Text.Json;
using System.Text.Json.Serialization;
using OrangeGuidanceTomestone.Helpers;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Pack {
    internal static SemaphoreSlim AllMutex { get; } = new(1, 1);
    internal static Pack[] All { get; set; } = [];
    private static readonly JsonSerializerOptions Options = new() {
        Converters = {
            new TemplateConverter(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static Dictionary<string, string> TemplatesZH { get; set; } = new();
    public static Dictionary<string, string> ConjunctionsZH { get; set; } = new();
    public static Dictionary<string, string> DictionaryZH { get; set; } = new();

    public string Name { get; init; }
    public Guid Id { get; init; }

    public Template[] Templates { get; init; }

    public string[]? Conjunctions { get; init; }
    public List<WordList>? Words { get; init; }

    internal static void UpdatePacks() {
        Task.Run(async () => {
            var resp = await ServerHelper.SendRequest(null, HttpMethod.Get, "/packs");
            var json = await resp.Content.ReadAsStringAsync();
            var packsEn = JsonSerializer.Deserialize<Pack[]>(json, Pack.Options)!;

            // read local zh json
            string jsonZh;
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "OrangeGuidanceTomestone.Resources.zh.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                jsonZh = reader.ReadToEnd();
            }

            var packsZh = JsonSerializer.Deserialize<Pack[]>(jsonZh, Pack.Options)!;

            foreach (var packZh in packsZh)
            {
                var index = packsEn.ToList().FindIndex(x => x.Id == packZh.Id);
                if (index != -1)
                {
                    var packEn = packsEn[index];
                    for (int i = 0; i < packZh.Templates.Length; i++)
                    {
                        TemplatesZH[packEn.Templates[i].Text] = packZh.Templates[i].Text;
                    }

                    for (int i = 0; i < packZh.Conjunctions.Length; i++)
                    {
                        ConjunctionsZH[packEn.Conjunctions[i]] = packZh.Conjunctions[i];
                    }

                    for (int i = 0; i < packZh.Words.Count; i++)
                    {
                        for (int j = 0; j < packZh.Words[i].Words.Length; j++)
                        {
                            DictionaryZH[packEn.Words[i].Words[j]] = packZh.Words[i].Words[j];
                        }
                    }

                    packsEn[index] = packZh;
                }
            }

            await AllMutex.WaitAsync();
            try {
                All = packsEn;
            } finally {
                AllMutex.Release();
            }
        });
    }
}

public class Template {
    [JsonPropertyName("template")]
    public string Text { get; init; }
    public string[]? Words { get; init; }
}

public class TemplateConverter : JsonConverter<Template> {
    private static JsonSerializerOptions RemoveSelf(JsonSerializerOptions old) {
        var newOptions = new JsonSerializerOptions(old);
        for (var i = 0; i < old.Converters.Count; i++) {
            if (old.Converters[i] is TemplateConverter) {
                newOptions.Converters.RemoveAt(i);
                break;
            }
        }

        return newOptions;
    }

    public override Template? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        switch (reader.TokenType) {
            case JsonTokenType.String: {
                    var template = reader.GetString() ?? throw new JsonException("template cannot be null");
                return new Template {
                        Text = template,
                        Words = null,
                    };
                }
            case JsonTokenType.StartObject: {
                    var newOptions = TemplateConverter.RemoveSelf(options);
                    return JsonSerializer.Deserialize<Template>(ref reader, newOptions);
                }
            default: {
                    throw new JsonException("unexpected template type");
                }
        }
    }

    public override void Write(Utf8JsonWriter writer, Template value, JsonSerializerOptions options) {
        if (value.Words == null) {
            JsonSerializer.Serialize(writer, value.Text, options);
        } else {
            var newOptions = TemplateConverter.RemoveSelf(options);
            JsonSerializer.Serialize(writer, value, newOptions);
        }
    }
}

[Serializable]
public class WordList {
    public string Name { get; init; }
    public string[] Words { get; init; }
}
