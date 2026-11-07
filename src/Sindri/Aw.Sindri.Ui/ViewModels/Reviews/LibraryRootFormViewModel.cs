using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Settings' "Add a folder" form (review page spec §6.3): a name, a folder to type or Browse for, the patterns not to read, and Save and approve. A 400 SND-LIB-001 says its detail under the field it names.</summary>
internal sealed class LibraryRootFormViewModel : ObservableObject
{
    private const string LibraryRefusal = "SND-LIB-001";

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly IFolderPicker _folders;
    private readonly Func<LibraryRootChanged, CancellationToken, Task> _saved;

    internal LibraryRootFormViewModel(Func<Uri?> url, ISindriApi api, IFolderPicker folders, Func<LibraryRootChanged, CancellationToken, Task> saved)
    {
        (_url, _api, _folders, _saved) = (url, api, folders, saved);
    }

#pragma warning disable CA1822, MA0041 // why: the view binds these as instance properties of the view model
    /// <summary>The form's heading.</summary>
    public string Title => Labels.LibraryFoldersFormTitle;

    /// <summary>The name field's label.</summary>
    public string NameLabel => Labels.LibraryFoldersNameLabel;

    /// <summary>The folder field's label.</summary>
    public string PathLabel => Labels.LibraryFoldersPathLabel;

    /// <summary>The deny field's label.</summary>
    public string DenyLabel => Labels.LibraryFoldersDenyLabel;

    /// <summary>The deny field's example.</summary>
    public string DenyPlaceholder => Labels.LibraryFoldersDenyPlaceholder;

    /// <summary>The chooser button's words.</summary>
    public string BrowseText => Labels.LibraryFoldersBrowse;

    /// <summary>The note under the fields.</summary>
    public string NoteText => Labels.LibraryFoldersFormNote;

    /// <summary>The Cancel button's words.</summary>
    public string CancelText => Labels.Cancel;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the form is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The vault's name as typed.</summary>
    public string Name
    {
        get;
        set
        {
            Set(ref field, value);
            NameError = string.Empty;
            OnChanged(nameof(CanSave));
        }
    } = string.Empty;

    /// <summary>The folder as typed or chosen.</summary>
    public string Path
    {
        get;
        set
        {
            Set(ref field, value);
            PathError = string.Empty;
            OnChanged(nameof(CanSave));
        }
    } = string.Empty;

    /// <summary>The patterns not to read, separated by commas.</summary>
    public string Deny
    {
        get;
        set
        {
            Set(ref field, value);
            DenyError = string.Empty;
        }
    } = string.Empty;

    /// <summary>What is wrong with the name; empty when nothing is.</summary>
    public string NameError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNameError));
        }
    } = string.Empty;

    /// <summary>What is wrong with the folder; empty when nothing is.</summary>
    public string PathError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasPathError));
        }
    } = string.Empty;

    /// <summary>What is wrong with the patterns; empty when nothing is.</summary>
    public string DenyError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasDenyError));
        }
    } = string.Empty;

    /// <summary>A refusal that belongs to no field (a session's SND-AUTH-002, an unreachable Sindri); empty when none.</summary>
    public string FormError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasFormError));
        }
    } = string.Empty;

    /// <summary>True when the name has an error.</summary>
    public bool HasNameError => NameError.Length > 0;

    /// <summary>True when the folder has an error.</summary>
    public bool HasPathError => PathError.Length > 0;

    /// <summary>True when the patterns have an error.</summary>
    public bool HasDenyError => DenyError.Length > 0;

    /// <summary>True when there is a form-level error.</summary>
    public bool HasFormError => FormError.Length > 0;

    /// <summary>True when this head can show a folder chooser.</summary>
    public bool CanBrowse => _folders.IsAvailable;

    /// <summary>True while the save is on its way.</summary>
    public bool IsSending
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSave));
            OnChanged(nameof(SaveText));
        }
    }

    /// <summary>True when Save and approve can be pressed: a name and a folder, and no save in flight.</summary>
    public bool CanSave => !IsSending && Name.Trim().Length > 0 && Path.Trim().Length > 0;

    /// <summary>The save button's words.</summary>
    public string SaveText => IsSending ? Labels.LibraryFoldersSaving : Labels.LibraryFoldersSave;

    /// <summary>Opens the form empty.</summary>
    internal void Open()
    {
        Clear();
        IsOpen = true;
    }

    /// <summary>Closes the form and forgets what was typed.</summary>
    internal void Cancel()
    {
        IsOpen = false;
        Clear();
    }

    /// <summary>Opens the folder chooser and puts the chosen folder in the field; no choice keeps what was typed.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the field is set.</returns>
    internal async Task Browse(CancellationToken cancellationToken)
    {
        var chosen = await _folders.Pick(Labels.LibraryFoldersBrowseTitle, cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            Path = chosen;
        }
    }

    /// <summary>Sends the folder with approve true, once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Save(CancellationToken cancellationToken)
    {
        if (!CanSave || _url() is not { } url)
        {
            return;
        }

        IsSending = true;
        Result<LibraryRootChanged> saved;
        try
        {
            saved = await _api.AddLibraryRoot(url, new LibraryRootAdd(Name.Trim(), Path.Trim(), DenyGlobs(), Approve: true), cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsSending = false;
        }

        if (saved.IsError)
        {
            Refuse(saved.Error);
            return;
        }

        IsOpen = false;
        Clear();
        await _saved(saved.Value, cancellationToken).ConfigureAwait(true);
    }

    private string[] DenyGlobs() => [.. Deny.Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private void Clear()
    {
        (Name, Path, Deny) = (string.Empty, string.Empty, string.Empty);
        FormError = string.Empty;
    }

    // SND-LIB-001's detail goes under the field the answer names; any other refusal (a session's 403) is the form's own line.
    private void Refuse(Error error)
    {
        (NameError, PathError, DenyError, FormError) = (string.Empty, string.Empty, string.Empty, string.Empty);
        switch (string.Equals(error.Code, LibraryRefusal, StringComparison.Ordinal) ? FieldOf(error) : null)
        {
            case "name":
                NameError = error.Message;
                break;
            case "path":
                PathError = error.Message;
                break;
            case "deny":
                DenyError = error.Message;
                break;
            default:
                FormError = Labels.InstanceRefused(error);
                break;
        }
    }

    private static string? FieldOf(Error error) =>
        error.Extensions?.Where(e => string.Equals(e.Key, "field", StringComparison.Ordinal)).Select(e => e.Value.GetString()).FirstOrDefault();
}
