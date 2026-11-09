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
    public string SizeText => View.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);   // stub: the formatter comes with the real commit

    /// <summary>The fill 0..1 (size over limit) when the resource has a limit; null otherwise.</summary>
    public double? Fill => View.LimitBytes is > 0 ? 0 : null;   // stub

    /// <summary>The size line: "1.2 MB of 5.0 MB" with a limit, the size alone without.</summary>
    public string SizeLine => SizeText;   // stub

    /// <summary>True while the row asks "Purge?" and waits for the answer.</summary>
    public bool IsConfirming { get; private set => Set(ref field, value); }

    /// <summary>The confirm's question; names the owner.</summary>
    public string ConfirmText => View.Kind;   // stub

    /// <summary>Asks first; nothing is sent.</summary>
    internal void AskPurge() => throw new NotSupportedException();

    /// <summary>Backs out of the question.</summary>
    internal void Keep() => throw new NotSupportedException();

    /// <summary>Sends the purge once, after the question was asked.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal Task ConfirmPurge(CancellationToken cancellationToken) => _purge(View.Owner, cancellationToken);
}
