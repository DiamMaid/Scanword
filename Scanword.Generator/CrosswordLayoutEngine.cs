using System;
using System.Collections.Generic;
using System.Linq;
using Scanword.Domain;

namespace Scanword.Generator;

public sealed class CrosswordLayoutEngine
{
    private readonly Dictionary<(int Row, int Col), char> _letters = new();
    private readonly HashSet<(int Row, int Col)> _acrossOwner = new();
    private readonly HashSet<(int Row, int Col)> _downOwner = new();
    private readonly HashSet<(int Row, int Col)> _noLetter = new();
    private readonly List<PlacedWord> _placed = new();

    private int _minRow;
    private int _minCol;
    private int _maxRow;
    private int _maxCol;
    private bool _bboxInitialized;
    private int _maxDimension;

    public CrosswordLayout? Generate(
        IReadOnlyList<WordEntry> dictionary,
        int targetWordCount,
        int maxDimension,
        Random random)
    {
        _maxDimension = maxDimension;

        List<WordEntry> pool = dictionary
            .Where(w => w.Word.Length >= 3 && w.Word.Length <= maxDimension - 1)
            .ToList();

        if (pool.Count < targetWordCount)
            return null;

        Shuffle(pool, random);

        if (!TryCommit(new Placement(pool[0], 0, 0, Direction.Right), requireIntersection: false))
            return null;

        foreach (WordEntry entry in pool.Skip(1))
        {
            if (_placed.Count >= targetWordCount)
                break;

            Placement? best = FindBestPlacement(entry, random);
            if (best is not null)
                TryCommit(best.Value, requireIntersection: true);
        }

        if (_placed.Count < targetWordCount)
            return null;

        return new CrosswordLayout(_placed.ToArray(), _minRow, _minCol, _maxRow, _maxCol);
    }

    private Placement? FindBestPlacement(WordEntry entry, Random random)
    {
        var seen = new HashSet<(int Row, int Col, Direction Direction)>();
        var feasible = new List<(Placement Placement, int Score)>();

        foreach (PlacedWord placed in _placed)
        {
            var (placedDr, placedDc) = DirectionHelper.GetDelta(placed.Direction);
            Direction newDirection = placed.Direction == Direction.Right ? Direction.Down : Direction.Right;
            var (newDr, newDc) = DirectionHelper.GetDelta(newDirection);

            for (int i = 0; i < placed.Entry.Word.Length; i++)
            {
                char anchor = placed.Entry.Word[i];
                for (int j = 0; j < entry.Word.Length; j++)
                {
                    if (entry.Word[j] != anchor)
                        continue;

                    int anchorRow = placed.Row + i * placedDr;
                    int anchorCol = placed.Col + i * placedDc;
                    int startRow = anchorRow - j * newDr;
                    int startCol = anchorCol - j * newDc;

                    if (!seen.Add((startRow, startCol, newDirection)))
                        continue;

                    var candidate = new Placement(entry, startRow, startCol, newDirection);
                    int score = Evaluate(candidate);
                    if (score > 0)
                        feasible.Add((candidate, score));
                }
            }
        }

        if (feasible.Count == 0)
            return null;

        int maxScore = feasible.Max(f => f.Score);
        List<Placement> top = feasible.Where(f => f.Score == maxScore).Select(f => f.Placement).ToList();
        return top[random.Next(top.Count)];
    }

    private int Evaluate(Placement p)
    {
        var (dr, dc) = DirectionHelper.GetDelta(p.Direction);
        int length = p.Entry.Word.Length;

        int clueRow = p.Row - dr;
        int clueCol = p.Col - dc;
        if (_letters.ContainsKey((clueRow, clueCol)))
            return -1;

        int endRow = p.Row + length * dr;
        int endCol = p.Col + length * dc;
        if (_letters.ContainsKey((endRow, endCol)))
            return -1;

        if (!FitsDimension(p, clueRow, clueCol))
            return -1;

        int intersections = 0;
        for (int i = 0; i < length; i++)
        {
            int row = p.Row + i * dr;
            int col = p.Col + i * dc;

            if (_letters.TryGetValue((row, col), out char existing))
            {
                if (existing != p.Entry.Word[i])
                    return -1;
                if (p.Direction == Direction.Right && _acrossOwner.Contains((row, col)))
                    return -1;
                if (p.Direction == Direction.Down && _downOwner.Contains((row, col)))
                    return -1;
                intersections++;
                continue;
            }

            if (_noLetter.Contains((row, col)))
                return -1;

            if (p.Direction == Direction.Right)
            {
                if (_letters.ContainsKey((row - 1, col)) || _letters.ContainsKey((row + 1, col)))
                    return -1;
            }
            else
            {
                if (_letters.ContainsKey((row, col - 1)) || _letters.ContainsKey((row, col + 1)))
                    return -1;
            }
        }

        return intersections;
    }

    private bool FitsDimension(Placement p, int clueRow, int clueCol)
    {
        var (dr, dc) = DirectionHelper.GetDelta(p.Direction);
        int lastRow = p.Row + (p.Entry.Word.Length - 1) * dr;
        int lastCol = p.Col + (p.Entry.Word.Length - 1) * dc;

        int minRow = _bboxInitialized ? Math.Min(_minRow, Math.Min(clueRow, p.Row)) : Math.Min(clueRow, p.Row);
        int minCol = _bboxInitialized ? Math.Min(_minCol, Math.Min(clueCol, p.Col)) : Math.Min(clueCol, p.Col);
        int maxRow = _bboxInitialized ? Math.Max(_maxRow, Math.Max(clueRow, lastRow)) : Math.Max(clueRow, lastRow);
        int maxCol = _bboxInitialized ? Math.Max(_maxCol, Math.Max(clueCol, lastCol)) : Math.Max(clueCol, lastCol);

        return maxCol - minCol + 1 <= _maxDimension && maxRow - minRow + 1 <= _maxDimension;
    }

    private bool TryCommit(Placement p, bool requireIntersection)
    {
        int score = Evaluate(p);
        if (score < 0 || (requireIntersection && score == 0))
            return false;

        var (dr, dc) = DirectionHelper.GetDelta(p.Direction);
        int length = p.Entry.Word.Length;
        int wordId = _placed.Count + 1;

        int clueRow = p.Row - dr;
        int clueCol = p.Col - dc;
        _noLetter.Add((clueRow, clueCol));
        _noLetter.Add((p.Row + length * dr, p.Col + length * dc));

        for (int i = 0; i < length; i++)
        {
            int row = p.Row + i * dr;
            int col = p.Col + i * dc;
            _letters[(row, col)] = p.Entry.Word[i];
            _noLetter.Remove((row, col));
            if (p.Direction == Direction.Right)
                _acrossOwner.Add((row, col));
            else
                _downOwner.Add((row, col));

            ExpandBbox(row, col);
        }

        ExpandBbox(clueRow, clueCol);
        _placed.Add(new PlacedWord(wordId, p.Entry, p.Row, p.Col, p.Direction, clueRow, clueCol));
        return true;
    }

    private void ExpandBbox(int row, int col)
    {
        if (!_bboxInitialized)
        {
            _minRow = _maxRow = row;
            _minCol = _maxCol = col;
            _bboxInitialized = true;
            return;
        }

        _minRow = Math.Min(_minRow, row);
        _minCol = Math.Min(_minCol, col);
        _maxRow = Math.Max(_maxRow, row);
        _maxCol = Math.Max(_maxCol, col);
    }

    private static void Shuffle<T>(List<T> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private readonly record struct Placement(WordEntry Entry, int Row, int Col, Direction Direction);
}
