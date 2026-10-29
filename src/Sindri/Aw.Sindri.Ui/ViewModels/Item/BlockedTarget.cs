namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>One denied target of an item, counted over its launches.</summary>
/// <param name="Kind">'network', 'policy' or 'path'.</param>
/// <param name="Target">The host, path or tool.</param>
/// <param name="Count">How many times, over every launch.</param>
/// <param name="Fix">'allow', 'never' or 'own-terminal'.</param>
/// <param name="Reason">Why, for 'never'.</param>
/// <param name="Launch">The launch with the most denials of it in <paramref name="Repo"/>: what Allow edits the repo of.</param>
/// <param name="Repo">The repo its launches ran in, or null from a server before the field; a target denied in two repos is two rows, so one Allow never claims to fix both.</param>
internal sealed record BlockedTarget(string Kind, string Target, int Count, string Fix, string Reason, string Launch, string? Repo = null)
{
    /// <summary>The target's identity across reads.</summary>
    internal string Key => Kind + "|" + Target + "|" + Repo;
}
