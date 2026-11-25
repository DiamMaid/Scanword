using System.Collections.Generic;

namespace Scanword.Domain;

public sealed record ScanwordLevel(
    string Id,
    int Version,
    int Width,
    int Height,
    Cell[] Cells,
    IReadOnlyList<Word> Words,
    IReadOnlyList<ClueReference> Clues,
    IReadOnlyList<ClueCellBinding> ClueCells,
    LevelMetadata Meta);