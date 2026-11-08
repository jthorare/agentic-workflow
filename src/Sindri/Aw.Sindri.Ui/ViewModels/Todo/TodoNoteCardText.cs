namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>Settings' Obsidian todo note card's own copy (<see cref="Labels"/>) as the view binds it.</summary>
internal sealed class TodoNoteCardText
{
    /// <summary>The one instance.</summary>
    internal static TodoNoteCardText Instance { get; } = new();

    /// <summary>The card's name.</summary>
    public string Name { get; } = Labels.TodoCardName;

    /// <summary>The line under the name.</summary>
    public string Intro { get; } = Labels.TodoCardIntro;

    /// <summary>The no-vault sentence.</summary>
    public string NoVaults { get; } = Labels.TodoCardNoVaults;

    /// <summary>The button beside it.</summary>
    public string OpenLibrary { get; } = Labels.TodoCardOpenLibrary;

    /// <summary>The vault box's name.</summary>
    public string Vault { get; } = Labels.TodoCardVault;

    /// <summary>The vault box's name for a screen reader.</summary>
    public string VaultName { get; } = Labels.TodoCardVaultName;

    /// <summary>The note box's name for a screen reader.</summary>
    public string NoteName { get; } = Labels.TodoCardNoteName;

    /// <summary>The note box's name.</summary>
    public string Note { get; } = Labels.TodoCardNote;

    /// <summary>The note box's hint.</summary>
    public string NotePlaceholder { get; } = Labels.TodoCardNotePlaceholder;

    /// <summary>Check note.</summary>
    public string Check { get; } = Labels.TodoCardCheck;

    /// <summary>Start syncing.</summary>
    public string Start { get; } = Labels.TodoCardStart;

    /// <summary>Start syncing (one way).</summary>
    public string StartOneWay { get; } = Labels.TodoCardStartOneWay;

    /// <summary>Import and start syncing.</summary>
    public string Import { get; } = Labels.TodoCardImport;

    /// <summary>Cancel.</summary>
    public string Cancel { get; } = Labels.TodoCardCancel;

    /// <summary>Change.</summary>
    public string Change { get; } = Labels.TodoCardChange;

    /// <summary>Stop syncing.</summary>
    public string Stop { get; } = Labels.TodoCardStop;

    /// <summary>Keep syncing.</summary>
    public string Keep { get; } = Labels.TodoCardKeep;
}
