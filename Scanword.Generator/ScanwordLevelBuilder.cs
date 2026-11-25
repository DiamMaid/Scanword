using System.Collections.Generic;
using System.Linq;
using Scanword.Domain;

namespace Scanword.Generator;

public static class ScanwordLevelBuilder
{
    public static ScanwordLevel Build(
        CrosswordLayout layout,
        string id,
        int version,
        Difficulty difficulty,
        string title,
        int estimatedMinutes)
    {
        int width = layout.Width;
        int height = layout.Height;
        int cellCount = width * height;

        var kinds = new CellKind[cellCount];
        var acrossIds = new int[cellCount];
        var downIds = new int[cellCount];
        var solutions = new char[cellCount];
        var clueIdsByCell = new Dictionary<int, List<int>>();

        foreach (PlacedWord placed in layout.Words)
        {
            var (dRow, dCol) = DirectionHelper.GetDelta(placed.Direction);

            for (int i = 0; i < placed.Entry.Word.Length; i++)
            {
                int row = placed.Row + i * dRow - layout.MinRow;
                int col = placed.Col + i * dCol - layout.MinCol;
                int index = row * width + col;

                kinds[index] = CellKind.Letter;
                solutions[index] = placed.Entry.Word[i];
                if (placed.Direction == Direction.Right)
                    acrossIds[index] = placed.WordId;
                else
                    downIds[index] = placed.WordId;
            }

            int clueIndex = (placed.ClueRow - layout.MinRow) * width + (placed.ClueCol - layout.MinCol);
            kinds[clueIndex] = CellKind.Clue;

            if (!clueIdsByCell.TryGetValue(clueIndex, out List<int>? clueIds))
            {
                clueIds = new List<int>();
                clueIdsByCell[clueIndex] = clueIds;
            }

            clueIds.Add(placed.WordId);
        }

        var cells = new Cell[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            cells[i] = kinds[i] switch
            {
                CellKind.Letter => new Cell(CellKind.Letter, acrossIds[i], downIds[i], solutions[i]),
                CellKind.Clue => new Cell(CellKind.Clue, 0, 0, '\0'),
                _ => new Cell(CellKind.Empty, 0, 0, '\0')
            };
        }

        Word[] words = layout.Words
            .Select(p => new Word(
                p.WordId,
                p.Row - layout.MinRow,
                p.Col - layout.MinCol,
                p.Direction,
                p.Entry.Word.Length,
                p.Entry.Word,
                p.WordId))
            .ToArray();

        ClueReference[] clues = layout.Words
            .Select(p => new ClueReference(p.WordId, p.Entry.Definition, p.Direction, p.WordId))
            .ToArray();

        ClueCellBinding[] clueCells = clueIdsByCell
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ClueCellBinding(kvp.Key, kvp.Value.OrderBy(x => x).ToArray()))
            .ToArray();

        var meta = new LevelMetadata(difficulty, title, "ru-RU", words.Length, estimatedMinutes);

        return new ScanwordLevel(id, version, width, height, cells, words, clues, clueCells, meta);
    }
}
