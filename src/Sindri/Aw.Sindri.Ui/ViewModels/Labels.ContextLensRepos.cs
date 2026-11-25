namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the lenses code band's words when it names the repos with no code index (#3861).</summary>
internal static partial class Labels
{
    /// <summary>The coverage sentence's words when, with several repos in scope, these repos have no code index yet.</summary>
    /// <param name="repos">The repos, in the answer's order; at least one.</param>
    /// <returns>"No code index for aw-split-panes yet." or "No code index for api, docs and web yet."</returns>
    internal static string ContextMapNamesNoIndexFor(IReadOnlyList<string> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);
        var names = repos.Count < 2 ? string.Join(", ", repos) : string.Join(", ", repos.Take(repos.Count - 1)) + " and " + repos[^1];
        return "No code index for " + names + " yet.";
    }
}
