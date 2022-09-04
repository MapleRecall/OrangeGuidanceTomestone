using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OrangeGuidanceTomestone;

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
internal class Message {
    public Guid Id { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    [JsonProperty("message")]
    public string Text { get; init; }

    public int PositiveVotes { get; init; }
    public int NegativeVotes { get; init; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);
}
