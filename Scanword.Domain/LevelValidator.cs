using System.Collections.Generic;
using System.Linq;

namespace Scanword.Domain;

public static class LevelValidator
{
    public static LevelValidationResult Validate(ScanwordLevel level)
    {
        var violations = new List<InvariantViolation>();

        int expectedLength = level.Width * level.Height;
        if (level.Cells.Length != expectedLength)
        {
            violations.Add(new InvariantViolation(1,
                $"Cells.Length ({level.Cells.Length}) != Width * Height ({expectedLength})"));
        }

        var existingWordIds = new HashSet<int>(level.Words.Select(w => w.Id));
        var existingClueIds = new HashSet<int>(level.Clues.Select(c => c.Id));

        foreach (Word word in level.Words)
        {
            var (dRow, dCol) = DirectionHelper.GetDelta(word.Direction);
            for (int pos = 0; pos < word.Length; pos++)
            {
                int row = word.StartRow + pos * dRow;
                int col = word.StartCol + pos * dCol;
                if (row < 0 || row >= level.Height || col < 0 || col >= level.Width)
                {
                    violations.Add(new InvariantViolation(7,
                        $"Word {word.Id}: position {pos} maps to ({row},{col}) outside grid ({level.Width}x{level.Height})"));
                }
            }
        }

        for (int i = 0; i < level.Cells.Length; i++)
        {
            Cell cell = level.Cells[i];

            if (cell.Kind == CellKind.Letter)
            {
                if (cell.AcrossWordId == 0 && cell.DownWordId == 0)
                {
                    violations.Add(new InvariantViolation(2,
                        $"Letter cell at index {i} (row={i / level.Width}, col={i % level.Width}) has no word membership"));
                }

                if (cell.AcrossWordId != 0 && !existingWordIds.Contains(cell.AcrossWordId))
                {
                    violations.Add(new InvariantViolation(5,
                        $"Cell {i} references non-existent AcrossWordId {cell.AcrossWordId}"));
                }
                if (cell.DownWordId != 0 && !existingWordIds.Contains(cell.DownWordId))
                {
                    violations.Add(new InvariantViolation(5,
                        $"Cell {i} references non-existent DownWordId {cell.DownWordId}"));
                }

                // Invariant 3: Solution must match Answer at cell's position in each owning word
                foreach (int wordId in GetWordIds(cell))
                {
                    Word? word = level.Words.FirstOrDefault(w => w.Id == wordId);
                    if (word == null) continue;

                    int pos = DirectionHelper.PositionInWord(word, i, level.Width);
                    if (pos < 0)
                    {
                        violations.Add(new InvariantViolation(3,
                            $"Letter cell {i} claims membership in word {wordId} but cell is not in the word's path"));
                    }
                    else if (pos < word.Answer.Length && cell.Solution != word.Answer[pos])
                    {
                        violations.Add(new InvariantViolation(3,
                            $"Letter cell {i} has Solution '{cell.Solution}' but word {wordId} has '{word.Answer[pos]}' at position {pos}"));
                    }
                }
            }

            }

        // Invariant 4: Clue cell is not in the word it defines; first letter is next cell by Arrow
        foreach (var binding in level.ClueCells)
        {
            foreach (int clueId in binding.ClueIds)
            {
                if (!existingClueIds.Contains(clueId))
                {
                    violations.Add(new InvariantViolation(5,
                        $"ClueCellBinding cell {binding.CellIndex} references non-existent ClueId {clueId}"));
                    continue;
                }

                ClueReference clue = level.Clues.First(c => c.Id == clueId);
                if (!existingWordIds.Contains(clue.WordId))
                {
                    violations.Add(new InvariantViolation(5,
                        $"ClueReference {clueId} references non-existent WordId {clue.WordId}"));
                    continue;
                }

                Word word = level.Words.First(w => w.Id == clue.WordId);

                // Clue cell must not be in the word it defines
                if (DirectionHelper.IsCellInWord(word, binding.CellIndex, level.Width))
                {
                    violations.Add(new InvariantViolation(4,
                        $"Clue cell {binding.CellIndex} is inside word {word.Id} which it defines"));
                }

                // First letter of the word must be the next cell in the Arrow direction from the clue cell
                int expectedFirstCell = NeighborCell(binding.CellIndex, clue.Arrow, level.Width, level.Height);
                int actualFirstCell = DirectionHelper.CellIndexAt(word, 0, level.Width);
                if (expectedFirstCell < 0 || expectedFirstCell != actualFirstCell)
                {
                    violations.Add(new InvariantViolation(4,
                        $"Clue cell {binding.CellIndex} arrow {clue.Arrow}: expected first letter of word {word.Id} at cell {expectedFirstCell} but found at {actualFirstCell}"));
                }
            }
        }

        // Invariant 6: Answer.Length == Length for each word
        foreach (Word word in level.Words)
        {
            if (word.Answer.Length != word.Length)
            {
                violations.Add(new InvariantViolation(6,
                    $"Word {word.Id}: Answer.Length ({word.Answer.Length}) != Length ({word.Length})"));
            }
        }

        return violations.Count == 0
            ? LevelValidationResult.Success
            : new LevelValidationResult(false, violations);
    }

    private static int[] GetWordIds(Cell cell)
    {
        if (cell.AcrossWordId != 0 && cell.DownWordId != 0)
            return [cell.AcrossWordId, cell.DownWordId];
        if (cell.AcrossWordId != 0)
            return [cell.AcrossWordId];
        if (cell.DownWordId != 0)
            return [cell.DownWordId];
        return [];
    }

    private static int NeighborCell(int cellIndex, Direction direction, int width, int height)
    {
        int row = cellIndex / width;
        int col = cellIndex % width;
        var (dRow, dCol) = DirectionHelper.GetDelta(direction);
        int newRow = row + dRow;
        int newCol = col + dCol;
        if (newRow < 0 || newRow >= height || newCol < 0 || newCol >= width)
            return -1;
        return newRow * width + newCol;
    }
}