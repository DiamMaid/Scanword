namespace Scanword.Domain;

public readonly record struct CellProgress(int CellIndex, char Letter, CellState State);