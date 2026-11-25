namespace Scanword.Domain;

public static class DirectionHelper
{
    public static (int dRow, int dCol) GetDelta(Direction direction) => direction switch
    {
        Direction.Right => (0, 1),
        Direction.Down => (1, 0),
        Direction.Left => (0, -1),
        Direction.Up => (-1, 0),
        _ => (0, 0)
    };

    public static int CellIndexAt(Word word, int position, int width)
    {
        var (dRow, dCol) = GetDelta(word.Direction);
        int row = word.StartRow + position * dRow;
        int col = word.StartCol + position * dCol;
        return row * width + col;
    }

    public static int PositionInWord(Word word, int cellIndex, int width)
    {
        for (int i = 0; i < word.Length; i++)
        {
            if (CellIndexAt(word, i, width) == cellIndex)
                return i;
        }
        return -1;
    }

    public static bool IsCellInWord(Word word, int cellIndex, int width)
    {
        return PositionInWord(word, cellIndex, width) >= 0;
    }
}