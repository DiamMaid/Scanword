namespace Scanword.Domain;

public enum HintResult
{
    Success,
    InsufficientCoins,
    InvalidCell,
    CellAlreadyRevealed,
    WordAlreadySolved,
    LevelCompleted
}