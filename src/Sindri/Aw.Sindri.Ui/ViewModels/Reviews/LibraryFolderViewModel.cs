using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One folder of Settings' Library card (review page spec §6.3, §16): its names and status, and the one control its kind allows.</summary>
internal sealed class LibraryFolderViewModel : ObservableObject
{
    private const string Aw = "aw";
    private const string Vault = "vault";
    private const string Repo = "repo";

    /// <summary>The row for a served folder.</summary>
    /// <param name="folder">The folder as served.</param>
    /// <param name="now">The time the status's age is counted from.</param>
    internal LibraryFolderViewModel(LibraryFolderView folder, DateTimeOffset now)
    {
        Name = folder.Name;
        KindText = folder.Kind switch
        {
            Aw => Labels.LibraryFoldersBuiltIn,
            Vault => Labels.LibraryFoldersVault,
            _ => Labels.LibraryFoldersFromRepos,
        };
        PathText = folder.Scope ?? folder.Path;
        IsOff = string.Equals(folder.Kind, Aw, StringComparison.Ordinal) && !folder.On;
        IsUnreadable = folder.Unreadable is { Length: > 0 };
        IsReading = !IsOff && !IsUnreadable && folder.ScannedAt is null;
        StatusText = Status(folder, now);
        CanRemove = string.Equals(folder.Kind, Vault, StringComparison.Ordinal);
        CanTurnOff = string.Equals(folder.Kind, Aw, StringComparison.Ordinal) && folder.On;
        CanTurnOn = IsOff;
        IsReadOnly = string.Equals(folder.Kind, Repo, StringComparison.Ordinal);
        AutomationName = string.Join(", ", new[] { Name, KindText, PathText, StatusText, IsReadOnly ? ReadOnlyText : null }.OfType<string>());
    }

    /// <summary>The folder's name: "aw", a vault's own or "repo:owner-name".</summary>
    public string Name { get; }

    /// <summary>The chip: built in, vault or from your repos.</summary>
    public string KindText { get; }

    /// <summary>The path line: a repo's "owner/name · docs/**", else the folder.</summary>
    public string PathText { get; }

    /// <summary>The status line: when it was read, that it is being read, why it can't be read, or that it is off.</summary>
    public string StatusText { get; }

    /// <summary>True while the folder has not been read yet.</summary>
    public bool IsReading { get; }

    /// <summary>True when the folder can't be read.</summary>
    public bool IsUnreadable { get; }

    /// <summary>True for the aw folder turned off.</summary>
    public bool IsOff { get; }

    /// <summary>True for a vault: it can be removed.</summary>
    public bool CanRemove { get; }

    /// <summary>True for the aw folder while it is on.</summary>
    public bool CanTurnOff { get; }

    /// <summary>True for the aw folder while it is off.</summary>
    public bool CanTurnOn { get; }

    /// <summary>True for a repo's folder: the repo list sets it.</summary>
    public bool IsReadOnly { get; }

    /// <summary>The line a repo's folder shows in place of a button.</summary>
#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property
    public string ReadOnlyText => Labels.LibraryFoldersReadOnly;
#pragma warning restore CA1822, MA0041

    /// <summary>The Remove button's words.</summary>
#pragma warning disable CA1822, MA0041 // why: as above
    public string RemoveText => Labels.LibraryFoldersRemove;

    /// <summary>The confirmation's Cancel button.</summary>
    public string CancelText => Labels.Cancel;

    /// <summary>The Turn off button's words.</summary>
    public string TurnOffText => Labels.LibraryFoldersTurnOff;

    /// <summary>The Turn on button's words.</summary>
    public string TurnOnText => Labels.LibraryFoldersTurnOn;
#pragma warning restore CA1822, MA0041

    /// <summary>The Remove button's screen-reader name.</summary>
    public string RemoveName => Labels.LibraryFoldersRemoveName(Name);

    /// <summary>The Turn off or Turn on button's screen-reader name.</summary>
    public string AwName => Labels.LibraryFoldersAwName(on: IsOff);

    /// <summary>The row's screen-reader name: everything the row says, in order.</summary>
    public string AutomationName { get; }

    /// <summary>True while Remove asks for its confirmation.</summary>
    public bool IsConfirmingRemove { get; private set => Set(ref field, value); }

    /// <summary>The confirmation's question.</summary>
    public string ConfirmText => Labels.LibraryFoldersRemoveAsk(Name);

    /// <summary>Asks for the removal's confirmation (a vault only).</summary>
    internal void AskRemove() => IsConfirmingRemove = CanRemove;

    /// <summary>Withdraws the question; nothing is sent.</summary>
    internal void CancelRemove() => IsConfirmingRemove = false;

    private static string Status(LibraryFolderView folder, DateTimeOffset now)
    {
        if (string.Equals(folder.Kind, Aw, StringComparison.Ordinal) && !folder.On)
        {
            return Labels.LibraryFoldersOff;
        }

        if (folder.Unreadable is { Length: > 0 } reason)
        {
            return Labels.LibraryFoldersUnreadable(reason);
        }

        return folder.ScannedAt is { } at ? Labels.LibraryFoldersScanned(now - at, folder.Docs, folder.Errors) : Labels.LibraryFoldersReading;
    }
}
