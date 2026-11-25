using System.Collections.Generic;

namespace Scanword.Domain;

public sealed class ScanwordEngine
{
    private readonly ScanwordLevel _level;
    private readonly CellState[] _cellStates;
    private readonly char[] _cellLetters;
    private readonly WordStatus[] _wordStatuses;
    private int _selectedWordId;
    private int _cursorCellIndex;
    private int _coinsSpent;
    private LevelStatus _levelStatus;
    private readonly int _hintCostPerLetter;
    private readonly int _hintCostPerWord;

    public ScanwordEngine(ScanwordLevel level, int hintCostPerLetter = 1, int hintCostPerWord = 3)
    {
        _level = level;
        _cellStates = new CellState[level.Cells.Length];
        _cellLetters = new char[level.Cells.Length];
        _wordStatuses = new WordStatus[level.Words.Count];
        _selectedWordId = 0;
        _cursorCellIndex = -1;
        _coinsSpent = 0;
        _levelStatus = LevelStatus.NotStarted;
        _hintCostPerLetter = hintCostPerLetter;
        _hintCostPerWord = hintCostPerWord;
    }

    public ScanwordLevel Level => _level;
    public IReadOnlyList<CellState> CellStates => _cellStates;
    public IReadOnlyList<char> CellLetters => _cellLetters;
    public IReadOnlyList<WordStatus> WordStatuses => _wordStatuses;
    public int SelectedWordId => _selectedWordId;
    public int CursorCellIndex => _cursorCellIndex;
    public int CoinsSpent => _coinsSpent;
    public int HintCostPerLetter => _hintCostPerLetter;
    public int HintCostPerWord => _hintCostPerWord;
    public LevelStatus LevelStatus => _levelStatus;

    public void SubmitLetter(int cellIndex, char letter)
    {
        if (_levelStatus == LevelStatus.Completed) return;
        if (cellIndex < 0 || cellIndex >= _level.Cells.Length) return;

        Cell cell = _level.Cells[cellIndex];
        if (cell.Kind != CellKind.Letter) return;
        if (_cellStates[cellIndex] == CellState.Confirmed) return;

        _cellLetters[cellIndex] = letter;
        _cellStates[cellIndex] = CellState.Filled;

        if (_levelStatus == LevelStatus.NotStarted)
            _levelStatus = LevelStatus.InProgress;

        int wordId = ResolveWordForCell(cellIndex);
        if (wordId == 0) return;

        _selectedWordId = wordId;
        AutoAdvanceCursor(wordId, cellIndex);
    }

    public void EraseAtCursor()
    {
        if (_cursorCellIndex < 0) return;
        if (_level.Cells[_cursorCellIndex].Kind != CellKind.Letter) return;
        if (_cellStates[_cursorCellIndex] == CellState.Confirmed) return;

        _cellLetters[_cursorCellIndex] = '\0';
        _cellStates[_cursorCellIndex] = CellState.Untouched;

        if (_selectedWordId > 0)
        {
            Word word = GetWordById(_selectedWordId)!;
            int pos = DirectionHelper.PositionInWord(word, _cursorCellIndex, _level.Width);
            if (pos > 0)
            {
                _cursorCellIndex = DirectionHelper.CellIndexAt(word, pos - 1, _level.Width);
            }
        }
    }

    public void SelectWord(int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= _level.Cells.Length) return;
        Cell cell = _level.Cells[cellIndex];

        int[] wordIds = GetWordIdsForCell(cell);
        if (wordIds.Length == 0) return;

        if (wordIds.Length == 1)
        {
            SelectSingleWord(wordIds[0]);
            return;
        }

        if (_selectedWordId == wordIds[0])
            SelectSingleWord(wordIds[1]);
        else if (_selectedWordId == wordIds[1])
            SelectSingleWord(wordIds[0]);
        else
            SelectSingleWord(wordIds[0]);
    }

    public bool ValidateWord(int wordId)
    {
        Word? word = GetWordById(wordId);
        if (word == null) return false;

        int wordIndex = GetWordIndexById(wordId);
        if (wordIndex < 0) return false;
        if (_wordStatuses[wordIndex] == WordStatus.Solved) return true;

        bool allCorrect = true;
        for (int i = 0; i < word.Length; i++)
        {
            int ci = DirectionHelper.CellIndexAt(word, i, _level.Width);
            if (_cellStates[ci] == CellState.Confirmed) continue;

            if (_cellLetters[ci] == word.Answer[i])
            {
                _cellStates[ci] = CellState.Confirmed;
            }
            else
            {
                _cellStates[ci] = CellState.Error;
                allCorrect = false;
            }
        }

        if (allCorrect)
        {
            _wordStatuses[wordIndex] = WordStatus.Solved;
            CheckLevelCompletion();
        }

        return allCorrect;
    }

    public HintOutcome OpenLetter(int cellIndex, int availableCoins)
    {
        if (_levelStatus == LevelStatus.Completed)
            return new HintOutcome(HintResult.LevelCompleted, 0);

        if (cellIndex < 0 || cellIndex >= _level.Cells.Length)
            return new HintOutcome(HintResult.InvalidCell, 0);

        Cell cell = _level.Cells[cellIndex];
        if (cell.Kind != CellKind.Letter)
            return new HintOutcome(HintResult.InvalidCell, 0);

        if (_cellStates[cellIndex] == CellState.Confirmed)
            return new HintOutcome(HintResult.CellAlreadyRevealed, 0);

        if (availableCoins < _hintCostPerLetter)
            return new HintOutcome(HintResult.InsufficientCoins, 0);

        _cellLetters[cellIndex] = cell.Solution;
        _cellStates[cellIndex] = CellState.Confirmed;
        _coinsSpent += _hintCostPerLetter;

        if (_levelStatus == LevelStatus.NotStarted)
            _levelStatus = LevelStatus.InProgress;

        CheckLevelCompletion();
        return new HintOutcome(HintResult.Success, _hintCostPerLetter);
    }

    public HintOutcome OpenWord(int wordId, int availableCoins)
    {
        if (_levelStatus == LevelStatus.Completed)
            return new HintOutcome(HintResult.LevelCompleted, 0);

        Word? word = GetWordById(wordId);
        if (word == null)
            return new HintOutcome(HintResult.WordAlreadySolved, 0);

        int wordIndex = GetWordIndexById(wordId);
        if (wordIndex < 0 || _wordStatuses[wordIndex] == WordStatus.Solved)
            return new HintOutcome(HintResult.WordAlreadySolved, 0);

        if (availableCoins < _hintCostPerWord)
            return new HintOutcome(HintResult.InsufficientCoins, 0);

        for (int i = 0; i < word.Length; i++)
        {
            int ci = DirectionHelper.CellIndexAt(word, i, _level.Width);
            _cellLetters[ci] = word.Answer[i];
            _cellStates[ci] = CellState.Confirmed;
        }
        _wordStatuses[wordIndex] = WordStatus.Solved;
        _coinsSpent += _hintCostPerWord;

        if (_levelStatus == LevelStatus.NotStarted)
            _levelStatus = LevelStatus.InProgress;

        CheckLevelCompletion();
        return new HintOutcome(HintResult.Success, _hintCostPerWord);
    }

    public LevelProgress CreateProgressSnapshot()
    {
        var cellProgressList = new List<CellProgress>();
        for (int i = 0; i < _level.Cells.Length; i++)
        {
            if (_cellStates[i] is CellState.Filled or CellState.Confirmed or CellState.Error)
            {
                cellProgressList.Add(new CellProgress(i, _cellLetters[i], _cellStates[i]));
            }
        }

        var wordProgressList = new WordProgress[_level.Words.Count];
        for (int i = 0; i < _level.Words.Count; i++)
        {
            wordProgressList[i] = new WordProgress(_level.Words[i].Id, _wordStatuses[i]);
        }

        return new LevelProgress(
            _level.Id,
            _level.Version,
            _levelStatus,
            cellProgressList.ToArray(),
            wordProgressList,
            _coinsSpent);
    }

    public static ScanwordEngine CreateFromProgress(ScanwordLevel level, LevelProgress progress)
    {
        var engine = new ScanwordEngine(level);
        engine.RestoreState(progress);
        return engine;
    }

    private void RestoreState(LevelProgress progress)
    {
        foreach (CellProgress cp in progress.Cells)
        {
            if (cp.CellIndex >= 0 && cp.CellIndex < _cellStates.Length)
            {
                _cellStates[cp.CellIndex] = cp.State;
                _cellLetters[cp.CellIndex] = cp.Letter;
            }
        }

        foreach (WordProgress wp in progress.Words)
        {
            int idx = GetWordIndexById(wp.WordId);
            if (idx >= 0)
                _wordStatuses[idx] = wp.Status;
        }

        _coinsSpent = progress.CoinsSpent;
        _levelStatus = progress.Status;
    }

    private int ResolveWordForCell(int cellIndex)
    {
        Cell cell = _level.Cells[cellIndex];

        if (_selectedWordId > 0)
        {
            if (cell.AcrossWordId == _selectedWordId || cell.DownWordId == _selectedWordId)
                return _selectedWordId;
        }

        if (cell.AcrossWordId != 0) return cell.AcrossWordId;
        if (cell.DownWordId != 0) return cell.DownWordId;
        return 0;
    }

    private void AutoAdvanceCursor(int wordId, int currentCellIndex)
    {
        Word? word = GetWordById(wordId);
        if (word == null) { _cursorCellIndex = -1; return; }

        int currentPos = DirectionHelper.PositionInWord(word, currentCellIndex, _level.Width);
        int nextCell = -1;
        for (int i = currentPos + 1; i < word.Length; i++)
        {
            int ci = DirectionHelper.CellIndexAt(word, i, _level.Width);
            if (_cellStates[ci] == CellState.Untouched)
            {
                nextCell = ci;
                break;
            }
        }

        if (nextCell >= 0)
        {
            _cursorCellIndex = nextCell;
            return;
        }

        bool allFilled = true;
        for (int i = 0; i < word.Length; i++)
        {
            int ci = DirectionHelper.CellIndexAt(word, i, _level.Width);
            if (_cellStates[ci] == CellState.Untouched)
            {
                allFilled = false;
                break;
            }
        }

        if (allFilled)
        {
            int wordIndex = GetWordIndexById(wordId);
            if (wordIndex >= 0 && _wordStatuses[wordIndex] != WordStatus.Solved)
            {
                bool solved = ValidateWord(wordId);
                if (!solved)
                {
                    for (int i = 0; i < word.Length; i++)
                    {
                        int ci = DirectionHelper.CellIndexAt(word, i, _level.Width);
                        if (_cellStates[ci] == CellState.Error)
                        {
                            _cursorCellIndex = ci;
                            return;
                        }
                    }
                    return;
                }
            }
        }

        int nextWordId = FindNextUnsolvedWord(wordId);
        if (nextWordId > 0)
        {
            _selectedWordId = nextWordId;
            Word nextWord = GetWordById(nextWordId)!;
            _cursorCellIndex = FindFirstUnfilledCell(nextWord);
        }
        else
        {
            _cursorCellIndex = -1;
        }
    }

    private int FindNextUnsolvedWord(int excludeWordId)
    {
        for (int i = 0; i < _level.Words.Count; i++)
        {
            if (_level.Words[i].Id != excludeWordId && _wordStatuses[i] == WordStatus.Unsolved)
                return _level.Words[i].Id;
        }
        return 0;
    }

    private int FindFirstUnfilledCell(Word word)
    {
        for (int i = 0; i < word.Length; i++)
        {
            int ci = DirectionHelper.CellIndexAt(word, i, _level.Width);
            if (_cellStates[ci] == CellState.Untouched)
                return ci;
        }
        return DirectionHelper.CellIndexAt(word, 0, _level.Width);
    }

    private void SelectSingleWord(int wordId)
    {
        _selectedWordId = wordId;
        Word? word = GetWordById(wordId);
        if (word != null)
            _cursorCellIndex = FindFirstUnfilledCell(word);
    }

    private void CheckLevelCompletion()
    {
        for (int i = 0; i < _wordStatuses.Length; i++)
        {
            if (_wordStatuses[i] != WordStatus.Solved)
                return;
        }
        _levelStatus = LevelStatus.Completed;
    }

    private Word? GetWordById(int wordId)
    {
        for (int i = 0; i < _level.Words.Count; i++)
        {
            if (_level.Words[i].Id == wordId)
                return _level.Words[i];
        }
        return null;
    }

    private int GetWordIndexById(int wordId)
    {
        for (int i = 0; i < _level.Words.Count; i++)
        {
            if (_level.Words[i].Id == wordId)
                return i;
        }
        return -1;
    }

    private static int[] GetWordIdsForCell(Cell cell)
    {
        if (cell.AcrossWordId != 0 && cell.DownWordId != 0)
            return [cell.AcrossWordId, cell.DownWordId];
        if (cell.AcrossWordId != 0)
            return [cell.AcrossWordId];
        if (cell.DownWordId != 0)
            return [cell.DownWordId];
        return [];
    }
}