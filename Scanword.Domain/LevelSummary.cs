namespace Scanword.Domain;

public sealed record LevelSummary(string Id, string Title, Difficulty Difficulty, int WordCount, int EstimatedMinutes);