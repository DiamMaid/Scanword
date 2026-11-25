namespace Scanword.Domain;

public sealed record ClueReference(int Id, string Text, Direction Arrow, int WordId);