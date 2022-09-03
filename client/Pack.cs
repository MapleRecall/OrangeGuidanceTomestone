namespace OrangeGuidanceTomestone;

[Serializable]
public class Pack {
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
