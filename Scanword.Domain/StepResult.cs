namespace Scanword.Domain;

public readonly record struct StepResult(
    bool Success,
    int NewCursorCellIndex,
    int[] ChangedCellIndices,
    int[] ChangedWordIds,
    bool IsLevelComplete);