namespace Scanword.ViewModels;

public sealed class LevelItemViewModel
{
    public LevelItemViewModel(int index, string title, string description)
    {
        Index = index;
        Title = title;
        Description = description;
    }

    public int Index { get; }
    public string Title { get; }
    public string Description { get; }
}
