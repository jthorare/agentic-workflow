namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>One denied target of an item, counted over its launches.</summary>
/// <param name="Kind">'network', 'policy' or 'path'.</param>
/// <param name="Target">The host, path or tool.</param>
/// <param name="Count">How many times, over every launch.</param>
/// <param name="Fix">'allow', 'never' or 'own-terminal'.</param>
/// <param name="Reason">Why, for 'never'.</param>
/// <param name="Launch">The launch with the most denials of it: what Allow edits the repo of.</param>
internal sealed record BlockedTarget(string Kind, string Target, int Count, string Fix, string Reason, string Launch)
{
    /// <summary>The target's identity across reads.</summary>
    internal string Key => Kind + "|" + Target;
}
