using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OrangeGuidanceTomestone;

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class MessageRequest {
    public uint Territory { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public Guid PackId { get; set; }
    public int Template1 { get; set; }
    public int? Word1List { get; set; }
    public int? Word1Word { get; set; }
    public int? Conjunction { get; set; }
    public int? Template2 { get; set; }
    public int? Word2List { get; set; }
    public int? Word2Word { get; set; }
}
