using System.Collections.Generic;

namespace Scanword.Generator;

public sealed record CrosswordLayout(
    IReadOnlyList<PlacedWord> Words,
    int MinRow,
    int MinCol,
    int MaxRow,
    int MaxCol)
{
    public int Width => MaxCol - MinCol + 1;
    public int Height => MaxRow - MinRow + 1;
}
