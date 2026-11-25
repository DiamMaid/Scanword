using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Scanword.Domain;

namespace Scanword.Controls;

public sealed class ScanwordBoard : Control
{
    public static readonly StyledProperty<ScanwordEngine?> EngineProperty =
        AvaloniaProperty.Register<ScanwordBoard, ScanwordEngine?>(nameof(Engine));

    public static readonly StyledProperty<int> RevisionProperty =
        AvaloniaProperty.Register<ScanwordBoard, int>(nameof(Revision));

    public static readonly StyledProperty<ICommand?> CellTappedCommandProperty =
        AvaloniaProperty.Register<ScanwordBoard, ICommand?>(nameof(CellTappedCommand));

    private static readonly IBrush ClueBackground = new SolidColorBrush(Color.Parse("#263238"));
    private static readonly IBrush ClueTextBrush = new SolidColorBrush(Color.Parse("#CFD8DC"));
    private static readonly IBrush LetterBackground = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush ConfirmedBackground = new SolidColorBrush(Color.Parse("#C8E6C9"));
    private static readonly IBrush ErrorBackground = new SolidColorBrush(Color.Parse("#FFCDD2"));
    private static readonly IBrush SelectedBackground = new SolidColorBrush(Color.Parse("#E3F2FD"));
    private static readonly IBrush CursorBackground = new SolidColorBrush(Color.Parse("#FFE082"));
    private static readonly IBrush GridLineBrush = new SolidColorBrush(Color.Parse("#78909C"));
    private static readonly IBrush CursorBorderBrush = new SolidColorBrush(Color.Parse("#F57C00"));
    private static readonly IBrush DefaultTextBrush = new SolidColorBrush(Color.Parse("#212121"));
    private static readonly IBrush ConfirmedTextBrush = new SolidColorBrush(Color.Parse("#1B5E20"));
    private static readonly IBrush ErrorTextBrush = new SolidColorBrush(Color.Parse("#B71C1C"));

    private static readonly Pen GridPen = new(GridLineBrush, 1);
    private static readonly Pen CursorPen = new(CursorBorderBrush, 2.5);

    private double _cellSize = 48;

    static ScanwordBoard()
    {
        AffectsRender<ScanwordBoard>(EngineProperty, RevisionProperty);
        AffectsMeasure<ScanwordBoard>(EngineProperty, RevisionProperty);
    }

    public ScanwordEngine? Engine
    {
        get => GetValue(EngineProperty);
        set => SetValue(EngineProperty, value);
    }

    public int Revision
    {
        get => GetValue(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    public ICommand? CellTappedCommand
    {
        get => GetValue(CellTappedCommandProperty);
        set => SetValue(CellTappedCommandProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ScanwordEngine? engine = Engine;
        if (engine is null)
            return base.MeasureOverride(availableSize);

        double availWidth = double.IsPositiveInfinity(availableSize.Width) ? 640 : availableSize.Width;
        double availHeight = double.IsPositiveInfinity(availableSize.Height) ? 640 : availableSize.Height;

        double fit = Math.Min(availWidth / engine.Level.Width, availHeight / engine.Level.Height);
        _cellSize = Math.Clamp(fit, 32, 110);

        return new Size(engine.Level.Width * _cellSize, engine.Level.Height * _cellSize);
    }

    public override void Render(DrawingContext context)
    {
        ScanwordEngine? engine = Engine;
        if (engine is null)
            return;

        ScanwordLevel level = engine.Level;
        double cs = _cellSize;
        HashSet<int> selectedCells = GetSelectedCells(level, engine.SelectedWordId);

        for (int i = 0; i < level.Cells.Length; i++)
        {
            Cell cell = level.Cells[i];
            if (cell.Kind == CellKind.Empty)
                continue;

            int row = i / level.Width;
            int col = i % level.Width;
            Rect rect = new Rect(col * cs, row * cs, cs, cs).Deflate(1);

            if (cell.Kind == CellKind.Clue)
                RenderClueCell(context, level, i, rect);
            else
                RenderLetterCell(context, engine, i, rect, selectedCells.Contains(i));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        ScanwordEngine? engine = Engine;
        if (engine is null)
            return;

        Point position = e.GetPosition(this);
        int col = (int)(position.X / _cellSize);
        int row = (int)(position.Y / _cellSize);

        if (row < 0 || row >= engine.Level.Height || col < 0 || col >= engine.Level.Width)
            return;

        int index = row * engine.Level.Width + col;
        if (engine.Level.Cells[index].Kind != CellKind.Letter)
            return;

        CellTappedCommand?.Execute(index);
    }

    private void RenderLetterCell(DrawingContext context, ScanwordEngine engine, int index, Rect rect, bool isSelected)
    {
        CellState state = engine.CellStates[index];
        bool isCursor = engine.CursorCellIndex == index;

        IBrush background = isCursor ? CursorBackground
            : state == CellState.Confirmed ? ConfirmedBackground
            : state == CellState.Error ? ErrorBackground
            : isSelected ? SelectedBackground
            : LetterBackground;

        double radius = Math.Max(2, rect.Width * 0.08);
        context.DrawRectangle(background, null, rect, radius, radius);
        context.DrawRectangle(null, isCursor ? CursorPen : GridPen, rect, radius, radius);

        char letter = engine.CellLetters[index];
        if (letter == '\0')
            return;

        IBrush textBrush = state == CellState.Confirmed ? ConfirmedTextBrush
            : state == CellState.Error ? ErrorTextBrush
            : DefaultTextBrush;

        FormattedText text = CreateText(letter.ToString(), rect.Width * 0.55, textBrush);
        Point origin = new(
            rect.X + (rect.Width - text.Width) / 2,
            rect.Y + (rect.Height - text.Height) / 2);
        context.DrawText(text, origin);
    }

    private void RenderClueCell(DrawingContext context, ScanwordLevel level, int index, Rect rect)
    {
        double radius = Math.Max(2, rect.Width * 0.08);
        context.DrawRectangle(ClueBackground, null, rect, radius, radius);
        context.DrawRectangle(null, GridPen, rect, radius, radius);

        List<int> clueIds = level.ClueCells
            .FirstOrDefault(b => b.CellIndex == index)?.ClueIds.ToList() ?? new List<int>();

        if (clueIds.Count == 0)
            return;

        if (clueIds.Count == 1)
        {
            RenderClueText(context, FindClue(level, clueIds[0]), rect.Deflate(3));
            return;
        }

        Rect top = new(rect.X + 3, rect.Y + 2, rect.Width - 6, rect.Height / 2 - 3);
        Rect bottom = new(rect.X + 3, rect.Y + rect.Height / 2 + 1, rect.Width - 6, rect.Height / 2 - 3);
        RenderClueText(context, FindClue(level, clueIds[0]), top);
        RenderClueText(context, FindClue(level, clueIds[1]), bottom);
    }

    private void RenderClueText(DrawingContext context, ClueReference? clue, Rect rect)
    {
        if (clue is null || rect.Width <= 0 || rect.Height <= 0)
            return;

        double fontSize = Math.Clamp(rect.Height * 0.34, 7, 15);
        FormattedText text = CreateText(ArrowGlyph(clue.Arrow) + " " + clue.Text, fontSize, ClueTextBrush);
        text.MaxTextWidth = rect.Width;
        text.MaxTextHeight = rect.Height;
        text.Trimming = TextTrimming.CharacterEllipsis;

        Point origin = new(rect.X, rect.Y + Math.Max(0, (rect.Height - text.Height) / 2));
        context.DrawText(text, origin);
    }

    private static FormattedText CreateText(string value, double fontSize, IBrush brush)
    {
        return new FormattedText(
            value,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);
    }

    private static ClueReference? FindClue(ScanwordLevel level, int clueId)
    {
        return level.Clues.FirstOrDefault(c => c.Id == clueId);
    }

    private static string ArrowGlyph(Direction direction) => direction switch
    {
        Direction.Right => "→",
        Direction.Down => "↓",
        Direction.Left => "←",
        Direction.Up => "↑",
        _ => "•"
    };

    private static HashSet<int> GetSelectedCells(ScanwordLevel level, int selectedWordId)
    {
        var result = new HashSet<int>();
        if (selectedWordId == 0)
            return result;

        Word? word = level.Words.FirstOrDefault(w => w.Id == selectedWordId);
        if (word is null)
            return result;

        for (int i = 0; i < word.Length; i++)
            result.Add(DirectionHelper.CellIndexAt(word, i, level.Width));

        return result;
    }
}
