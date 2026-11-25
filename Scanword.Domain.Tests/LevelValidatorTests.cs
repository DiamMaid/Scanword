namespace Scanword.Domain.Tests;

public class LevelValidatorTests
{
    [Fact]
    public void ValidLevelPassesAllInvariants()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        LevelValidationResult result = LevelValidator.Validate(level);
        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Invariant1_CellsLengthMustEqualWidthTimesHeight()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        ScanwordLevel badLevel = level with { Cells = new Cell[10] };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 1);
    }

    [Fact]
    public void Invariant2_LetterCellMustBelongToAtLeastOneWord()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        Cell[] cells = level.Cells.ToArray();
        cells[20] = new Cell(CellKind.Letter, AcrossWordId: 0, DownWordId: 0, Solution: 'А');
        ScanwordLevel badLevel = level with { Cells = cells };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 2 && v.Message.Contains("20"));
    }

    [Fact]
    public void Invariant3_SolutionMustMatchAnswerAtPositionInEveryOwningWord()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        Cell[] cells = level.Cells.ToArray();
        // Cell [12] is intersection: AcrossWordId=3 (word "КОТ", position 1='О'), DownWordId=2 (word "ОС", position 0='О')
        cells[12] = cells[12] with { Solution = 'А' };
        ScanwordLevel badLevel = level with { Cells = cells };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 3);
    }

    [Fact]
    public void Invariant4_ClueCellMustNotBeInsideItsOwnWord()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        // Clue at cell [1] defines word 1 (Across, cells [2],[3]). Move clue to cell [2] (inside word 1).
        Cell[] cells = level.Cells.ToArray();
        cells[1] = new Cell(CellKind.Empty, 0, 0, '\0');
        cells[2] = new Cell(CellKind.Clue, AcrossWordId: 1, DownWordId: 0, Solution: 'О');

        var clueCells = new ClueCellBinding[]
        {
            new(CellIndex: 2, ClueIds: [10]),
            new(CellIndex: 7, ClueIds: [20]),
            new(CellIndex: 10, ClueIds: [30]),
        };
        ScanwordLevel badLevel = level with { Cells = cells, ClueCells = clueCells };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 4);
    }

    [Fact]
    public void Invariant4_FirstLetterMustBeNextCellByArrow()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        // Change clue 20 arrow from Down to Right — now next cell from [7] by Right is [8], not [12]
        var clues = level.Clues.ToArray();
        clues[1] = clues[1] with { Arrow = Direction.Right };
        ScanwordLevel badLevel = level with { Clues = clues };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 4 && v.Message.Contains("arrow"));
    }

    [Fact]
    public void Invariant5_AllReferencedIdsMustExist()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        Cell[] cells = level.Cells.ToArray();
        cells[2] = cells[2] with { AcrossWordId = 99 };
        ScanwordLevel badLevel = level with { Cells = cells };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 5);
    }

    [Fact]
    public void Invariant6_AnswerLengthMustEqualWordLength()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        var words = level.Words.ToArray();
        words[0] = words[0] with { Answer = "О" };
        ScanwordLevel badLevel = level with { Words = words };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 6);
    }

    [Fact]
    public void Invariant7_WordExtendsBeyondGrid()
    {
        ScanwordLevel level = TestFixture.CreateValidLevel();
        var words = level.Words.ToArray();
        words[1] = words[1] with { Length = 5, Answer = "ОСОСО" };
        ScanwordLevel badLevel = level with { Words = words };

        LevelValidationResult result = LevelValidator.Validate(badLevel);
        Assert.False(result.IsValid);
        Assert.Contains(result.Violations, v => v.InvariantNumber == 7);
    }

    [Fact]
    public void DoubleClueCell_PassesValidation()
    {
        ScanwordLevel level = TestFixture.CreateLevelWithDoubleClueCell();
        LevelValidationResult result = LevelValidator.Validate(level);
        Assert.True(result.IsValid);
    }
}