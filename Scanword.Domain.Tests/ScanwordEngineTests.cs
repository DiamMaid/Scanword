namespace Scanword.Domain.Tests;

public class ScanwordEngineTests
{
    private static ScanwordLevel CreateLevel() => TestFixture.CreateValidLevel();

    [Fact]
    public void SubmitLetter_FillsCellAndAutoAdvancesCursor()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);

        engine.SubmitLetter(11, 'К');

        Assert.Equal(CellState.Filled, engine.CellStates[11]);
        Assert.Equal('К', engine.CellLetters[11]);
        Assert.Equal(12, engine.CursorCellIndex);
    }

    [Fact]
    public void SubmitLetter_OnLastCellOfWord_JumpsToNextUnsolvedWord()
    {
        ScanwordEngine engine = new(CreateLevel());

        engine.SelectWord(2);
        Assert.Equal(1, engine.SelectedWordId);

        engine.SubmitLetter(2, 'О');
        Assert.Equal(3, engine.CursorCellIndex);

        engine.SubmitLetter(3, 'К');
        Assert.NotEqual(1, engine.SelectedWordId);
        Assert.NotEqual(-1, engine.CursorCellIndex);
    }

    [Fact]
    public void SubmitLetter_AutoCompletesLevelWithoutExplicitValidate()
    {
        ScanwordEngine engine = new(CreateLevel());

        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(12, 'О');
        engine.SubmitLetter(13, 'Т');
        engine.SubmitLetter(2, 'О');
        engine.SubmitLetter(3, 'К');
        engine.SubmitLetter(17, 'С');

        Assert.Equal(LevelStatus.Completed, engine.LevelStatus);
        Assert.Equal(-1, engine.CursorCellIndex);
    }

    [Fact]
    public void SubmitLetter_AutoValidateFails_StaysInWord()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);

        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(12, 'А');
        engine.SubmitLetter(13, 'Т');

        Assert.Equal(3, engine.SelectedWordId);
        Assert.True(engine.CursorCellIndex == 12);
        Assert.Equal(CellState.Error, engine.CellStates[12]);
    }

    [Fact]
    public void SelectWord_IntersectionTogglesDirection()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(12);
        int first = engine.SelectedWordId;
        Assert.True(first == 2 || first == 3);

        engine.SelectWord(12);
        int second = engine.SelectedWordId;
        Assert.NotEqual(first, second);

        engine.SelectWord(12);
        int third = engine.SelectedWordId;
        Assert.Equal(first, third);
    }

    [Fact]
    public void SubmitLetter_RepeatInputUpdatesLetter()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);

        engine.SubmitLetter(11, 'К');
        Assert.Equal('К', engine.CellLetters[11]);

        engine.SubmitLetter(11, 'М');
        Assert.Equal('М', engine.CellLetters[11]);
        Assert.Equal(CellState.Filled, engine.CellStates[11]);
    }

    [Fact]
    public void EraseAtCursor_MovesBackwardInWordDirection()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);
        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(12, 'О');

        engine.EraseAtCursor();
        Assert.Equal(12, engine.CursorCellIndex);

        engine.EraseAtCursor();
        Assert.Equal(11, engine.CursorCellIndex);
        Assert.Equal(CellState.Untouched, engine.CellStates[12]);
    }

    [Fact]
    public void SubmitLetter_InClueOrEmptyCell_IsIgnored()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SubmitLetter(1, 'А');
        Assert.Equal(CellState.Untouched, engine.CellStates[1]);

        engine.SubmitLetter(0, 'Б');
        Assert.Equal(CellState.Untouched, engine.CellStates[0]);
    }

    [Fact]
    public void SubmitLetter_InConfirmedCell_IsIgnored()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.OpenLetter(11, 10);

        engine.SubmitLetter(11, 'Д');
        Assert.Equal('К', engine.CellLetters[11]);
        Assert.Equal(CellState.Confirmed, engine.CellStates[11]);
    }

    [Fact]
    public void SubmitLetter_WhenLevelCompleted_IsIgnored()
    {
        ScanwordEngine engine = new(CreateLevel());
        CompleteAllWords(engine);

        Assert.Equal(LevelStatus.Completed, engine.LevelStatus);
        engine.SubmitLetter(2, 'А');
        Assert.Equal(CellState.Confirmed, engine.CellStates[2]);
    }

    [Fact]
    public void ValidateWord_MarksCorrectCellsConfirmed()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);
        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(12, 'О');
        engine.SubmitLetter(13, 'Т');

        bool result = engine.ValidateWord(3);
        Assert.True(result);
        Assert.Equal(CellState.Confirmed, engine.CellStates[11]);
        Assert.Equal(CellState.Confirmed, engine.CellStates[12]);
        Assert.Equal(CellState.Confirmed, engine.CellStates[13]);
        Assert.Equal(WordStatus.Solved, engine.WordStatuses[2]);
    }

    [Fact]
    public void ValidateWord_MarksWrongCellsAsError()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);
        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(12, 'А');
        engine.SubmitLetter(13, 'Т');

        bool result = engine.ValidateWord(3);
        Assert.False(result);
        Assert.Equal(CellState.Confirmed, engine.CellStates[11]);
        Assert.Equal(CellState.Error, engine.CellStates[12]);
        Assert.Equal(CellState.Confirmed, engine.CellStates[13]);
    }

    [Fact]
    public void OpenLetter_WithSufficientCoins_RevealsCell()
    {
        ScanwordEngine engine = new(CreateLevel());
        HintOutcome outcome = engine.OpenLetter(11, 10);

        Assert.Equal(HintResult.Success, outcome.Result);
        Assert.Equal(1, outcome.CoinsSpent);
        Assert.Equal(1, engine.CoinsSpent);
        Assert.Equal('К', engine.CellLetters[11]);
        Assert.Equal(CellState.Confirmed, engine.CellStates[11]);
    }

    [Fact]
    public void OpenLetter_WithInsufficientCoins_Fails()
    {
        ScanwordEngine engine = new(CreateLevel());
        HintOutcome outcome = engine.OpenLetter(11, 0);

        Assert.Equal(HintResult.InsufficientCoins, outcome.Result);
        Assert.Equal(0, outcome.CoinsSpent);
    }

    [Fact]
    public void OpenLetter_OutOfBounds_ReturnsInvalidCell()
    {
        ScanwordEngine engine = new(CreateLevel());
        HintOutcome outcome = engine.OpenLetter(-1, 10);

        Assert.Equal(HintResult.InvalidCell, outcome.Result);
    }

    [Fact]
    public void OpenLetter_OnClueCell_ReturnsInvalidCell()
    {
        ScanwordEngine engine = new(CreateLevel());
        HintOutcome outcome = engine.OpenLetter(1, 10);

        Assert.Equal(HintResult.InvalidCell, outcome.Result);
    }

    [Fact]
    public void OpenWord_RevealsAllLettersInWord()
    {
        ScanwordEngine engine = new(CreateLevel());
        HintOutcome outcome = engine.OpenWord(3, 10);

        Assert.Equal(HintResult.Success, outcome.Result);
        Assert.Equal(3, outcome.CoinsSpent);
        Assert.Equal('К', engine.CellLetters[11]);
        Assert.Equal('О', engine.CellLetters[12]);
        Assert.Equal('Т', engine.CellLetters[13]);
        Assert.Equal(WordStatus.Solved, engine.WordStatuses[2]);
    }

    [Fact]
    public void OpenLetter_WhenLevelCompleted_Fails()
    {
        ScanwordEngine engine = new(CreateLevel());
        CompleteAllWords(engine);

        HintOutcome outcome = engine.OpenLetter(11, 10);
        Assert.Equal(HintResult.LevelCompleted, outcome.Result);
    }

    [Fact]
    public void CursorSkipsFilledAndErrorCells()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);

        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(13, 'Т');
        engine.SubmitLetter(12, 'О');

        Assert.NotEqual(13, engine.CursorCellIndex);
        Assert.Equal(1, engine.SelectedWordId);
        Assert.Equal(2, engine.CursorCellIndex);
    }

    [Fact]
    public void CreateProgressSnapshot_And_RestoreFromProgress_Roundtrip()
    {
        ScanwordEngine engine = new(CreateLevel());
        engine.SelectWord(11);
        engine.SubmitLetter(11, 'К');
        engine.SubmitLetter(12, 'О');
        engine.OpenLetter(13, 10);

        LevelProgress snapshot = engine.CreateProgressSnapshot();

        ScanwordEngine restored = ScanwordEngine.CreateFromProgress(CreateLevel(), snapshot);
        Assert.Equal('К', restored.CellLetters[11]);
        Assert.Equal('О', restored.CellLetters[12]);
        Assert.Equal('Т', restored.CellLetters[13]);
        Assert.Equal(CellState.Filled, restored.CellStates[11]);
        Assert.Equal(CellState.Filled, restored.CellStates[12]);
        Assert.Equal(CellState.Confirmed, restored.CellStates[13]);
        Assert.Equal(LevelStatus.InProgress, restored.LevelStatus);
        Assert.Equal(1, restored.CoinsSpent);
    }

    private static void CompleteAllWords(ScanwordEngine engine)
    {
        engine.OpenWord(1, 10);
        engine.OpenWord(2, 10);
        engine.OpenWord(3, 10);
    }
}