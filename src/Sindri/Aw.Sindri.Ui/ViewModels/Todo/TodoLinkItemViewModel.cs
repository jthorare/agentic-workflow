namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>One line of the link list (todo spec §3.3): a link's ref, the deep link it opens and whether J and K stand on it.</summary>
/// <param name="linkRef">What the owner typed: "acme/api#12", "ENG-12".</param>
/// <param name="href">The deep link.</param>
internal sealed class TodoLinkItemViewModel(string linkRef, string href) : ObservableObject
{
    /// <summary>The ref shown.</summary>
    public string Ref { get; } = linkRef;

    /// <summary>The deep link the line opens.</summary>
    public string Href { get; } = href;

    /// <summary>True on the line J, K and Enter act on.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }
}
