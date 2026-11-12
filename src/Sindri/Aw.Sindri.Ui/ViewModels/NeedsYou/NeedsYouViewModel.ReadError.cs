using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The failed first read of the list (<see cref="NeedsYouViewModel"/>, continued).</summary>
internal sealed partial class NeedsYouViewModel
{
    /// <summary>The failed first read as the shared failed-read record (#3056); the one source for <see cref="ReadError"/> and <see cref="HasReadError"/>. Null once a list has been read.</summary>
    internal FailedRead? ReadFailure { get; private set; }

    /// <summary>True until the first list is read, or its read fails: Home shows skeleton rows instead of an empty line.</summary>
    internal bool IsLoading => !_loaded && ReadFailure is null;

    /// <summary>The line Home shows when the first read failed; empty once a list has been read.</summary>
    internal string ReadError => ReadFailure?.Words ?? "";

    /// <summary>True when the first read failed and no list has been read since: Home shows <see cref="ReadError"/> and Retry in place of the skeleton.</summary>
    internal bool HasReadError => !_loaded && ReadFailure is not null;

    /// <summary>Says the first read failed. After a good read the rows stay as they were, so this changes nothing then.</summary>
    /// <param name="line">The words to show.</param>
    /// <param name="retry">What the Retry button does: read the list again.</param>
    internal void ReadFailed(string line, Func<CancellationToken, Task> retry)
    {
        if (_loaded)
        {
            return;
        }

        ReadFailure = new FailedRead(line, retry);
        foreach (var name in new[] { nameof(IsLoading), nameof(ReadFailure), nameof(ReadError), nameof(HasReadError), nameof(EmptyLine) })
        {
            OnChanged(name);
        }
    }
}
