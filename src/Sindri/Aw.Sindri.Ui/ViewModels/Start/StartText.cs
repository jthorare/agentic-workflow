namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>The Start form's own copy (<see cref="Labels"/>) as the view binds it; see Shell.ShellText.</summary>
internal sealed class StartText
{
    /// <summary>The one instance.</summary>
    internal static StartText Instance { get; } = new();

    /// <summary><see cref="Labels.StartWorkflow"/>.</summary>
    public string Title { get; } = Labels.StartWorkflow;

    /// <summary><see cref="Labels.StartRecipe"/>.</summary>
    public string Recipe { get; } = Labels.StartRecipe;

    /// <summary><see cref="Labels.StartItem"/>.</summary>
    public string Item { get; } = Labels.StartItem;

    /// <summary><see cref="Labels.StartPickCaption"/>.</summary>
    public string PickCaption { get; } = Labels.StartPickCaption;

    /// <summary><see cref="Labels.StartPickItem"/>.</summary>
    public string PickItem { get; } = Labels.StartPickItem;

    /// <summary><see cref="Labels.ChooseFile"/>.</summary>
    public string ChooseFile { get; } = Labels.ChooseFile;

    /// <summary><see cref="Labels.StartNewAttempt"/>.</summary>
    public string NewAttempt { get; } = Labels.StartNewAttempt;

    /// <summary><see cref="Labels.Cancel"/>.</summary>
    public string Cancel { get; } = Labels.Cancel;

    /// <summary><see cref="Labels.StartSending"/>.</summary>
    public string Sending { get; } = Labels.StartSending;

    /// <summary><see cref="Labels.StartLoading"/>.</summary>
    public string Loading { get; } = Labels.StartLoading;

    /// <summary><see cref="Labels.StartNoRecipes"/>.</summary>
    public string NoRecipes { get; } = Labels.StartNoRecipes;
}
