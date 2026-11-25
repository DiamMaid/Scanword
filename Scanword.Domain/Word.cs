namespace Scanword.Domain;

public sealed record Word(int Id, int StartRow, int StartCol, Direction Direction, int Length, string Answer, int ClueId);