namespace Scanword.Domain;

public sealed record LevelProgress(
    string LevelId,
    int LevelVersion,
    LevelStatus Status,
    CellProgress[] Cells,
    WordProgress[] Words,
    int CoinsSpent);