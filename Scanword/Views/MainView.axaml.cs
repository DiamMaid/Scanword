using Avalonia.Controls;
using Avalonia.Input;
using Scanword.ViewModels;

namespace Scanword.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
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
