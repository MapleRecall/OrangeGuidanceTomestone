using Newtonsoft.Json;
using OrangeGuidanceTomestone.Helpers;

namespace OrangeGuidanceTomestone;

[Serializable]
public class Pack {
    internal static SemaphoreSlim AllMutex { get; } = new(1, 1);
    internal static Pack[] All { get; set; } = [];

    public string Name { get; init; }
    public Guid Id { get; init; }

    [JsonConverter(typeof(TemplateConverter))]
    public ITemplate[] Templates { get; init; }

    public string[]? Conjunctions { get; init; }
    public List<WordList>? Words { get; init; }

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

public interface ITemplate {
    public string Template { get; }
    public string[]? Words { get; }
}

public class BasicTemplate : ITemplate {
    public string Template { get; init; }
    public string[]? Words => null;
}

[Serializable]
public class WordListTemplate : ITemplate {
    public string Template { get; init; }
    public string[] Words { get; init; }
}

public class TemplateConverter : JsonConverter<ITemplate>
{
    public override ITemplate? ReadJson(JsonReader reader, Type objectType, ITemplate? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.String) {
            var template = reader.ReadAsString();
            if (template == null) {
                return null;
            }

            return new BasicTemplate {
                Template = template,
            };
        } else if (reader.TokenType == JsonToken.StartObject) {
            return serializer.Deserialize<WordListTemplate>(reader);
        } else {
            throw new JsonReaderException("unexpected template kind");
        }
    }

    public override void WriteJson(JsonWriter writer, ITemplate? value, JsonSerializer serializer) {
        if (value is BasicTemplate basic) {
            serializer.Serialize(writer, basic.Template);
        } else if (value is WordListTemplate wordList) {
            serializer.Serialize(writer, wordList);
        } else {
            throw new JsonWriterException("unexpected template kind");
        }
    }
}

[Serializable]
public class WordList {
    public string Name { get; init; }
    public string[] Words { get; init; }
}
