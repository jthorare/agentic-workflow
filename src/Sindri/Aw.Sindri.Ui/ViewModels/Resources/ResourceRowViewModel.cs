using Aw.Sindri.Ui.Api.Resources;

namespace Aw.Sindri.Ui.ViewModels.Resources;

/// <summary>One resource on the Resources page: owner, kind, name and size in words, and a Purge that asks first, naming the owner.</summary>
internal sealed class ResourceRowViewModel : ObservableObject
{
    private readonly Func<string, CancellationToken, Task> _purge;
    private bool _busy;

    internal ResourceRowViewModel(ResourceView view, Func<string, CancellationToken, Task> purge)
    {
        (View, _purge) = (view, purge);
    }

    /// <summary>The resource as served.</summary>
    internal ResourceView View { get; }

    /// <summary>Who holds it.</summary>
    public string OwnerText => View.Owner;

    /// <summary>The kind in words.</summary>
    public string KindText => View.Kind;

    /// <summary>The name.</summary>
    public string NameText => View.Name;

    /// <summary>The size, human-readable ("1.2 MB").</summary>
    public string SizeText => ByteSize.Format(View.Size, trimZero: true);

    /// <summary>The fill 0..1 (size over limit) when the resource has a limit; null otherwise.</summary>
    public double? Fill => View.LimitBytes is > 0 ? Math.Clamp((double)View.Size / View.LimitBytes.Value, 0, 1) : null;

    /// <summary>The size line: "1.2 MB of 5.0 MB" with a limit, the size alone without.</summary>
    public string SizeLine => View.LimitBytes is { } limit ? Labels.ResourceSizeOf(SizeText, ByteSize.Format(limit, trimZero: false)) : SizeText;

    /// <summary>True while the row asks "Purge?" and waits for the answer.</summary>
    public bool IsConfirming { get; private set => Set(ref field, value); }

    /// <summary>The confirm's question; names the owner.</summary>
    public string ConfirmText => Labels.ResourcePurgeAsk(View.Owner);

    /// <summary>Asks first, naming the owner; nothing is sent.</summary>
    internal void AskPurge() => IsConfirming = !_busy;

    /// <summary>Backs out of the question.</summary>
    internal void Keep() => IsConfirming = false;

    /// <summary>Sends the purge once, after the question was asked. A second call while one is in flight sends nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task ConfirmPurge(CancellationToken cancellationToken)
    {
        if (!IsConfirming || _busy)
        {
            return;
        }

        _busy = true;
        try
        {
            await _purge(View.Owner, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _busy = false;
            IsConfirming = false;
        }
    }
}
