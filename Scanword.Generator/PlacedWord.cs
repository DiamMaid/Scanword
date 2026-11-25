using Scanword.Domain;

namespace Scanword.Generator;

public sealed record PlacedWord(
    int WordId,
    WordEntry Entry,
    int Row,
    int Col,
    Direction Direction,
    int ClueRow,
    int ClueCol);
