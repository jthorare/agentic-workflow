namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The failed first read of the list (<see cref="NeedsYouViewModel"/>, continued).</summary>
internal sealed partial class NeedsYouViewModel
{
    /// <summary>True until the first list is read, or its read fails: Home shows skeleton rows instead of an empty line.</summary>
    internal bool IsLoading => !_loaded && ReadError.Length == 0;

    /// <summary>The line Home shows when the first read failed; empty once a list has been read.</summary>
    internal string ReadError { get; private set; } = "";

    /// <summary>True when the first read failed and no list has been read since: Home shows <see cref="ReadError"/> and Retry in place of the skeleton.</summary>
    internal bool HasReadError => !_loaded && ReadError.Length > 0;

    /// <summary>Says the first read failed. After a good read the rows stay as they were, so this changes nothing then.</summary>
    /// <param name="line">The words to show.</param>
    internal void ReadFailed(string line)
    {
        if (_loaded)
        {
            return;
        }

        ReadError = line;
        foreach (var name in new[] { nameof(IsLoading), nameof(ReadError), nameof(HasReadError), nameof(EmptyLine) })
        {
            OnChanged(name);
        }
    }
}
