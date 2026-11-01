namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A concept the page sheet can relate an action to: its id (what is written) and its name (what is read).</summary>
/// <param name="Id">The concept's id.</param>
/// <param name="Name">The concept's name.</param>
/// <param name="Pages">How many pages express the concept (0 when unknown); breaks a tie when a page name matches several.</param>
internal sealed record ConceptChoice(string Id, string Name, int Pages = 0)
{
    private const string KeyPrefix = "concept:";

    /// <summary>The map key of the catch-all concept for the unassigned nodes; it is no concept a page can relate to.</summary>
    internal const string OtherKey = "concept:other";

    /// <summary>Gets the id a page is written with: a concept card's map key ("concept:scheduling") without its prefix ("scheduling"); an id with no prefix stays as it is.</summary>
    /// <param name="key">The map key or the bare id.</param>
    /// <returns>The bare id.</returns>
    internal static string BareId(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.StartsWith(KeyPrefix, StringComparison.Ordinal) ? key[KeyPrefix.Length..] : key;
    }
}
