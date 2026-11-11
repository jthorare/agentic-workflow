namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>One tag of a fact, with the action that forgets every fact carrying it. The action is off while a forget of the store is in flight.</summary>
internal sealed class MemoryTagViewModel : ObservableObject
{
    private readonly BusyFlag _busy;

    /// <summary>The tag over its forget action.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="forget">Sends the forget for this tag.</param>
    /// <param name="busy">The store's forget flag.</param>
    internal MemoryTagViewModel(string tag, Func<CancellationToken, Task> forget, BusyFlag busy)
    {
        (Tag, Forget, _busy) = (tag, forget, busy);
        _busy.PropertyChanged += (_, _) => OnChanged(nameof(CanForget));
    }

    /// <summary>The tag.</summary>
    public string Tag { get; }

    /// <summary>Sends the forget for this tag.</summary>
    internal Func<CancellationToken, Task> Forget { get; }

    /// <summary>False while a forget of the store is in flight.</summary>
    public bool CanForget => _busy.IsFree;

    /// <summary>The action's words: "Forget all tagged deploys".</summary>
    public string ForgetText => Labels.MemoryForgetTag(Tag);
}
