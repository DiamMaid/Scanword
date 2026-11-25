using System;
using Avalonia.Controls;
using Avalonia.Input;
using Scanword.ViewModels;

namespace Scanword.Views;

public partial class MainView : UserControl
{
    private const double MinZoom = 0.75;
    private const double MaxZoom = 4.0;

    private double _pinchBaseZoom = 1.0;
    private bool _pinching;

    public MainView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();

        Board.GestureRecognizers.Add(new PinchGestureRecognizer());
        Board.Pinch += OnPinch;
        Board.PinchEnded += (_, _) => _pinching = false;

        Board.DoubleTapped += (_, _) => (DataContext as MainViewModel)?.ResetZoomCommand.Execute(null);
        Board.PointerCaptureLost += (_, _) => _pinching = false;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (!_pinching)
        {
            _pinchBaseZoom = vm.Zoom;
            _pinching = true;
        }

        vm.Zoom = Math.Clamp(_pinchBaseZoom * e.Scale, MinZoom, MaxZoom);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (DataContext is not MainViewModel vm || string.IsNullOrEmpty(e.Text))
            return;

        char letter = char.ToUpperInvariant(e.Text[0]);
        if (letter is < 'А' or > 'Я')
            return;

        vm.InputLetterCommand.Execute(letter);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not MainViewModel vm)
            return;

        if (e.Key == Key.Back)
        {
            vm.EraseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
