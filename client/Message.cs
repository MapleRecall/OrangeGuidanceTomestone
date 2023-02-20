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
    public float Yaw { get; init; }

    [JsonProperty("message")]
    public string Text { get; init; }

    public int PositiveVotes { get; set; }
    public int NegativeVotes { get; set; }
    public int UserVote { get; set; }

    public int Glyph { get; set; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
internal class MessageWithTerritory {
    public Guid Id { get; init; }
    public uint Territory { get; init; }
    public uint? Ward { get; init; }
    public uint? Plot { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public float Yaw { get; init; }

    [JsonProperty("message")]
    public string Text { get; init; }

    public int PositiveVotes { get; init; }
    public int NegativeVotes { get; init; }
    public int UserVote { get; set; }

    public int Glyph { get; set; }
    public bool IsHidden { get; set; }

    internal Vector3 Position => new(this.X, this.Y, this.Z);

    internal static MessageWithTerritory From(Message message, uint territory) {
        return new MessageWithTerritory {
            Id = message.Id,
            Territory = territory,
            X = message.X,
            Y = message.Y,
            Z = message.Z,
            Yaw = message.Yaw,
            Text = message.Text,
            PositiveVotes = message.PositiveVotes,
            NegativeVotes = message.NegativeVotes,
            UserVote = message.UserVote,
            Glyph = message.Glyph,
            IsHidden = false,
        };
    }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
internal class ErrorMessage {
    public string Code { get; set; }
    public string Message { get; set; }
}

[Serializable]
[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
internal class MyMessages {
    public uint Extra { get; set; }
    public MessageWithTerritory[] Messages { get; set; }
}
