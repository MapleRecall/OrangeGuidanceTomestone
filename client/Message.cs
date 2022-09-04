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
    public int UserVote { get; set; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
internal class MessageWithTerritory {
    public Guid Id { get; init; }
    public uint Territory { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    [JsonProperty("message")]
    public string Text { get; init; }

    public int PositiveVotes { get; init; }
    public int NegativeVotes { get; init; }
    public int UserVote { get; set; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);

    internal static MessageWithTerritory From(Message message, uint territory) {
        return new MessageWithTerritory {
            Id = message.Id,
            Territory = territory,
            X = message.X,
            Y = message.Y,
            Z = message.Z,
            Text = message.Text,
            PositiveVotes = message.PositiveVotes,
            NegativeVotes = message.NegativeVotes,
            UserVote = message.UserVote,
        };
    }
}
