namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>One "an action of this kind is in flight" flag. A second caller that can't enter does nothing, and the buttons bound to <see cref="IsFree"/> are off meanwhile.</summary>
internal sealed class BusyFlag : ObservableObject
{
    /// <summary>True when no action is in flight.</summary>
    public bool IsFree { get; private set => Set(ref field, value); } = true;

    /// <summary>Takes the flag.</summary>
    /// <returns>True when it was free and is now taken; false when an action is already in flight.</returns>
    internal bool TryEnter()
    {
        if (!IsFree)
        {
            return false;
        }

        IsFree = false;
        return true;
    }

    /// <summary>Frees the flag.</summary>
    internal void Exit() => IsFree = true;
}
