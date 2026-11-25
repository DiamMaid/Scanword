namespace Scanword.Domain;

public sealed record LevelMetadata(Difficulty Difficulty, string Title, string Locale, int WordCount, int EstimatedMinutes);