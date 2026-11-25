using Aw.Sindri.Ui.Api.Resources;

namespace Aw.Sindri.Ui.ViewModels.Resources;

/// <summary>One resource on the Resources page: owner, kind, name and size in words, and a Purge that asks first, naming the owner.</summary>
internal sealed class ResourceRowViewModel : ObservableObject
{
    private readonly Func<string, CancellationToken, Task> _purge;

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
    public string SizeText => View.Size is { } size ? ByteSize.Format(size) : Labels.ResourceUnmeasured;

    /// <summary>The fill 0..1 (size over limit) when the resource has a limit; null otherwise.</summary>
    public double? Fill => View is { LimitBytes: > 0, Size: { } size } ? Math.Clamp((double)size / View.LimitBytes.Value, 0, 1) : null;

    /// <summary>The size line: "1.2 MB of 5 MB" with a limit, the size alone without.</summary>
    public string SizeLine => View is { LimitBytes: > 0, Size: not null } ? Labels.ResourceSizeOf(SizeText, ByteSize.Format(View.LimitBytes.Value)) : SizeText;

    /// <summary>True while the row asks "Purge?" and waits for the answer.</summary>
    public bool IsConfirming
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(ShowActions));
        }
    }

    /// <summary>True when the Purge action shows (not while the row asks).</summary>
    public bool ShowActions => !IsConfirming;

    /// <summary>True when the resource has a limit, so the bar shows.</summary>
    public bool HasFill => Fill is not null;

    /// <summary>The bar's value, 0 to 1.</summary>
    public double FillValue => Fill ?? 0;

    /// <summary>The row's accessible name: owner, kind, name and the size line.</summary>
    public string AccessibleName => $"{OwnerText}, {KindText}, {NameText}, {SizeLine}";

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The Purge action's words.</summary>
    public string PurgeText => Labels.ResourcePurge;

    /// <summary>The confirm button's words.</summary>
    public string ConfirmButtonText => Labels.ResourcePurgeConfirm;

    /// <summary>The Keep button's words.</summary>
    public string KeepText => Labels.ResourcePurgeKeep;
#pragma warning restore CA1822, MA0041

    /// <summary>The Purge action's accessible name.</summary>
    public string PurgeName => Labels.ResourceActionName(Labels.ResourcePurge, OwnerText);

    /// <summary>The confirm button's accessible name.</summary>
    public string ConfirmName => Labels.ResourceActionName(Labels.ResourcePurgeConfirm, OwnerText);

    /// <summary>The Keep button's accessible name.</summary>
    public string KeepName => Labels.ResourceActionName(Labels.ResourcePurgeKeep, OwnerText);

    /// <summary>The confirm's question; names the owner.</summary>
    public string ConfirmText => Labels.ResourcePurgeAsk(View.Owner);

    /// <summary>Asks first, naming the owner; nothing is sent.</summary>
    internal void AskPurge() => IsConfirming = true;

    /// <summary>Backs out of the question.</summary>
    internal void Keep() => IsConfirming = false;

    /// <summary>Sends the purge after the question was asked. The page sends one purge per owner at a time, so a second call while one is in flight sends nothing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task ConfirmPurge(CancellationToken cancellationToken)
    {
        if (!IsConfirming)
        {
            return;
        }

        try
        {
            await _purge(View.Owner, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsConfirming = false;
        }
    }
}
