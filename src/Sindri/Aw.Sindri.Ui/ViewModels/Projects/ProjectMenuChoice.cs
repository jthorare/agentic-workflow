namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>One choice of the Sort or the Group menu: its words, its hint and whether it is the one in force.</summary>
internal sealed class ProjectMenuChoice : ObservableObject
{
    internal ProjectMenuChoice(ProjectMenu menu, int value, string text, string hint)
    {
        (Menu, Value, Text, Hint) = (menu, value, text, hint);
    }

    /// <summary>The menu it belongs to.</summary>
    internal ProjectMenu Menu { get; }

    /// <summary>The <see cref="ProjectSort"/> or <see cref="ProjectGrouping"/> it stands for, as a number.</summary>
    internal int Value { get; }

    /// <summary>The words, as the menu and its button say them.</summary>
    public string Text { get; }

    /// <summary>The hint beside the words; empty for none.</summary>
    public string Hint { get; }

    /// <summary>True for the choice in force: it carries the check.</summary>
    public bool IsOn { get; internal set => Set(ref field, value); }
}
