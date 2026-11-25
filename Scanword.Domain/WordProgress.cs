namespace Scanword.Domain;

public readonly record struct WordProgress(int WordId, WordStatus Status);