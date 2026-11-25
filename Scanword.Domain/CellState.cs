namespace Scanword.Domain;

public enum CellState : byte
{
    Untouched = 0,
    Filled = 1,
    Confirmed = 2,
    Error = 3
}