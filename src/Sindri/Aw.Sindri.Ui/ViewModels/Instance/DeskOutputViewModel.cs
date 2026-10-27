using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One required output of a desk task, as the sheet asks for it (UI spec: outputs are file picks, at most 1 MiB, contents sent): a path box and a Browse button.</summary>
internal sealed class DeskOutputViewModel : ObservableObject
{
    /// <summary>The most bytes an output may hold (API: 1 MiB).</summary>
    internal const int MaxBytes = 1024 * 1024;

    private readonly IFilePicker _files;
    private readonly Func<string, int, CancellationToken, Task<PickedFile?>> _read;
    private PickedFile? _picked;

    /// <summary>The input for one required output.</summary>
    /// <param name="key">The output's key ('fact:output').</param>
    /// <param name="files">Where Browse chooses a file.</param>
    /// <param name="read">Reads the file a typed path names; null reads the file system.</param>
    internal DeskOutputViewModel(string key, IFilePicker files, Func<string, int, CancellationToken, Task<PickedFile?>>? read = null)
    {
        Key = key;
        _files = files;
        _read = read ?? FilePathReader.Read;
        Caption = Labels.OutputCaption(key);
        BoxName = Labels.OutputBoxName(key);
        BrowseName = Labels.OutputBrowseName(key);
    }

    /// <summary>The output's key.</summary>
    public string Key { get; }

    /// <summary>The words above the box.</summary>
    public string Caption { get; }

    /// <summary>The box's accessible name.</summary>
    public string BoxName { get; }

    /// <summary>The Browse button's accessible name.</summary>
    public string BrowseName { get; }

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The Browse button's words.</summary>
    public string BrowseText => Labels.BrowseOutput;
#pragma warning restore CA1822, MA0041

    /// <summary>The path typed, or the name of the file Browse chose.</summary>
    public string Path
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            _picked = null;   // a typed path is read at the press; a chosen file is kept as it was read
            Set(ref field, value);
            Error = string.Empty;
            OnChanged(nameof(IsSet));
        }
    } = string.Empty;

    /// <summary>True once there is a path or a chosen file.</summary>
    public bool IsSet => Path.Trim().Length > 0;

    /// <summary>The sentence under the box when the file can't be used, or empty.</summary>
    public string Error
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasError));
        }
    } = string.Empty;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Asks for a file and keeps it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the choice is shown.</returns>
    internal async Task Browse(CancellationToken cancellationToken)
    {
        if (await _files.Pick(Labels.ChooseOutputTitle, MaxBytes, cancellationToken).ConfigureAwait(true) is not { } picked)
        {
            return;   // none chosen: what was there stays
        }

        Path = picked.Name;
        _picked = picked;
    }

    /// <summary>Reads the file the box stands for.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or null when the file can't be used (the reason shows under the box).</returns>
    internal async Task<byte[]?> Content(CancellationToken cancellationToken)
    {
        var typed = Path.Trim();
        var file = _picked ?? (typed.Length == 0 ? null : await _read(typed, MaxBytes, cancellationToken).ConfigureAwait(true));
        if (file is null)
        {
            Error = Labels.OutputUnreadable(typed);
            return null;
        }

        if (file.Size > MaxBytes)
        {
            Error = Labels.OutputTooLarge(Key);
            return null;
        }

        Error = string.Empty;
        return file.Content.ToArray();
    }
}
