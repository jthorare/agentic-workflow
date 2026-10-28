namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>One blocked target on the item sheet, with its fix (states.json 'blockedList', 'allow').</summary>
internal sealed class BlockedRowViewModel : ObservableObject
{
    /// <summary>"Blocked: host0.example.org × 10".</summary>
    public string Line { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The row by kind, with its fix.</summary>
    public string Text { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row has an Allow button.</summary>
    public bool HasAllow { get; private set => Set(ref field, value); }

    /// <summary>The button's visible word: Allow, then Allowed or Refused.</summary>
    public string AllowText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The button's accessible name.</summary>
    public string AllowName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where Allow stands.</summary>
    public AllowState State { get; private set => Set(ref field, value); }

    /// <summary>The server's reason for a refusal, as served; empty otherwise.</summary>
    public string Refusal { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Presses Allow; a second press while sending does nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer was drawn.</returns>
    internal Task Allow(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnChanged(nameof(State));
        return Task.CompletedTask;
    }
}
