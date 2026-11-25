namespace Scanword.Domain;

public readonly record struct Cell(CellKind Kind, int AcrossWordId, int DownWordId, char Solution);