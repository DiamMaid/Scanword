namespace Scanword.Domain.Tests;

public static class TestFixture
{
    public static ScanwordLevel CreateValidLevel()
    {
        const int width = 5;
        const int height = 5;
        int cellCount = width * height;

        // Grid layout (5×5):
        //   col0  col1  col2  col3  col4
        // r0 E     C1→   L1w1  L2w1  E     (cells 0-4)
        // r1 E     E     C2↓   E     E     (cells 5-9)
        // r2 C3→   L1w3  L1w2  L2w3  E     (cells 10-14)
        // r3 E     E     L2w2  E     E     (cells 15-19)
        // r4 E     E     E     E     E     (cells 20-24)

        var cells = new Cell[cellCount];

        // Word 1: Across, Start=(0,2), Length=2, "ОК"
        cells[2] = new Cell(CellKind.Letter, AcrossWordId: 1, DownWordId: 0, Solution: 'О');
        cells[3] = new Cell(CellKind.Letter, AcrossWordId: 1, DownWordId: 0, Solution: 'К');

        // Word 2: Down, Start=(2,2), Length=2, "ОС"
        cells[12] = new Cell(CellKind.Letter, AcrossWordId: 3, DownWordId: 2, Solution: 'О');
        cells[17] = new Cell(CellKind.Letter, AcrossWordId: 0, DownWordId: 2, Solution: 'С');

        // Word 3: Across, Start=(2,1), Length=3, "КОТ"
        cells[11] = new Cell(CellKind.Letter, AcrossWordId: 3, DownWordId: 0, Solution: 'К');
        // cells[12] already set above with AcrossWordId=3, DownWordId=2
        cells[13] = new Cell(CellKind.Letter, AcrossWordId: 3, DownWordId: 0, Solution: 'Т');

        // Clue cells
        cells[1] = new Cell(CellKind.Clue, 0, 0, '\0');
        cells[7] = new Cell(CellKind.Clue, 0, 0, '\0');
        cells[10] = new Cell(CellKind.Clue, 0, 0, '\0');

        var words = new Word[]
        {
            new(Id: 1, StartRow: 0, StartCol: 2, Direction: Direction.Right, Length: 2, Answer: "ОК", ClueId: 10),
            new(Id: 2, StartRow: 2, StartCol: 2, Direction: Direction.Down, Length: 2, Answer: "ОС", ClueId: 20),
            new(Id: 3, StartRow: 2, StartCol: 1, Direction: Direction.Right, Length: 3, Answer: "КОТ", ClueId: 30),
        };

        var clues = new ClueReference[]
        {
            new(Id: 10, Text: "Выражение согласия", Arrow: Direction.Right, WordId: 1),
            new(Id: 20, Text: "Жалящее насекомое", Arrow: Direction.Down, WordId: 2),
            new(Id: 30, Text: "Пушистый питомец", Arrow: Direction.Right, WordId: 3),
        };

        var clueCells = new ClueCellBinding[]
        {
            new(CellIndex: 1, ClueIds: [10]),
            new(CellIndex: 7, ClueIds: [20]),
            new(CellIndex: 10, ClueIds: [30]),
        };

        var meta = new LevelMetadata(Difficulty.Easy, "Тестовый уровень", "ru", 3, 2);

        return new ScanwordLevel("test-1", 1, width, height, cells, words, clues, clueCells, meta);
    }

    public static ScanwordLevel CreateLevelWithDoubleClueCell()
    {
        const int width = 3;
        const int height = 3;
        int cellCount = width * height;

        var cells = new Cell[cellCount];

        cells[0] = new Cell(CellKind.Clue, 0, 0, '\0');
        cells[1] = new Cell(CellKind.Letter, AcrossWordId: 1, DownWordId: 0, Solution: 'А');
        cells[2] = new Cell(CellKind.Letter, AcrossWordId: 1, DownWordId: 0, Solution: 'Б');
        cells[3] = new Cell(CellKind.Letter, AcrossWordId: 0, DownWordId: 2, Solution: 'В');
        cells[6] = new Cell(CellKind.Letter, AcrossWordId: 0, DownWordId: 2, Solution: 'Г');

        var words = new Word[]
        {
            new(Id: 1, StartRow: 0, StartCol: 1, Direction: Direction.Right, Length: 2, Answer: "АБ", ClueId: 10),
            new(Id: 2, StartRow: 1, StartCol: 0, Direction: Direction.Down, Length: 2, Answer: "ВГ", ClueId: 20),
        };

        var clues = new ClueReference[]
        {
            new(Id: 10, Text: "Первое слово", Arrow: Direction.Right, WordId: 1),
            new(Id: 20, Text: "Второе слово", Arrow: Direction.Down, WordId: 2),
        };

        var clueCells = new ClueCellBinding[]
        {
            new(CellIndex: 0, ClueIds: [10, 20]),
        };

        var meta = new LevelMetadata(Difficulty.Easy, "Двойная подсказка", "ru", 2, 1);

        return new ScanwordLevel("double-clue-1", 1, width, height, cells, words, clues, clueCells, meta);
    }
}