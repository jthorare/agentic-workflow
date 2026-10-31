using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Pages;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context page list [--suggested] [--concept C] [--repo R] [--json]': a client of GET /sindri/context/pages. No flag lists the pages a person named; --suggested lists the ones the map offers
/// that nobody named. Exit 0 with an answer (an empty list is one); 1 for attention (not running, no map, an unknown concept or repo, sessions off); 2 for usage or a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class ContextPageVerbs(ControlPlaneClient client, Output output)
{
    private const string Fix = "(fix: sindri context page list [--suggested] [--concept C] [--repo R] [--json])";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            return ContextExit.For(parsed.Error, await output.Error(parsed.Error).ConfigureAwait(false));
        }

        var o = parsed.Value;
        var answer = await client.Get<PagesView>("/sindri/context/pages" + Query(o), ct).ConfigureAwait(false);
        return answer.IsError ? ContextExit.For(answer.Error, await output.Error(answer.Error).ConfigureAwait(false))
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(Text(answer.Value.Value, o.Suggested), 0).ConfigureAwait(false);
    }

    /// <summary>The list as text: one block per page (name, key, marks, code), its actions indented with their concepts; an empty list says what to do.</summary>
    /// <param name="view">The answer.</param>
    /// <param name="suggested">True when the list asked for suggested pages.</param>
    /// <returns>The text.</returns>
    internal static string Text(PagesView view, bool suggested)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Pages.Count == 0)
        {
            return suggested ? "No suggested pages found.\n" : "No pages named yet. Name one in the app, or see sindri context page list --suggested.\n";
        }

        var text = new StringBuilder();
        foreach (var page in view.Pages)
        {
            _ = text.AppendLine(page.Name).AppendLine(ContextLensText.Invariant($"  {page.Key.Value}  ·  {Mark(page.Mark)}  ·  {Code(page.Code)}"));
            foreach (var action in page.Actions)
            {
                var concepts = action.Concepts.Count == 0 ? string.Empty : "  ->  " + string.Join(", ", action.Concepts);
                _ = text.AppendLine(ContextLensText.Invariant($"    {action.Name}  ({Mark(action.Mark)}, {Code(action.Code)}){concepts}"));
            }
        }

        if (view.Truncated)
        {
            _ = text.AppendLine(ContextLensText.Invariant($"Showing {view.Pages.Count} of {view.Total} pages."));
        }

        return text.ToString();
    }

    private static string Mark(string mark) => string.Equals(mark, "owner", StringComparison.Ordinal) ? "Set by a person" : "Suggested";

    private static string Code(bool found) => found ? "Code found" : "No code found yet";

    private static string Query(Options o)
    {
        var parts = new List<string> { o.Suggested ? "marks=suggested" : "marks=owner" };
        if (o.Concept is not null)
        {
            parts.Add("concept=" + Uri.EscapeDataString(o.Concept));
        }

        if (o.Repo is not null)
        {
            parts.Add("repo=" + Uri.EscapeDataString(o.Repo));
        }

        return "?" + string.Join('&', parts);
    }

    /// <summary>'list' then the flags in any order; anything else (another verb, a word, an unknown flag, a flag without its value) is SND-CONTEXT-006, exit 1.</summary>
    private static Kernel.Result<Options> Parse(string[] args)
    {
        if (args is not ["list", .. var flags])
        {
            return Invalid("the page verb is 'list'");
        }

        var (suggested, json, concept, repo) = (false, false, (string?)null, (string?)null);
        for (var i = 0; i < flags.Length; i++)
        {
            switch (flags[i])
            {
                case "--suggested":
                    suggested = true;
                    break;
                case "--json":
                    json = true;
                    break;
                case "--concept" when i + 1 < flags.Length && concept is null:
                    concept = flags[++i];
                    break;
                case "--repo" when i + 1 < flags.Length && repo is null:
                    repo = flags[++i];
                    break;
                case var other:
                    return Invalid($"'{other}' is not a page list flag (or it needs one value)");
            }
        }

        return new Options(suggested, concept, repo, json);
    }

    private static Kernel.Error Invalid(string reason) => ContextErrors.QueryInvalid.With(reason + " " + Fix);

    private sealed record Options(bool Suggested, string? Concept, string? Repo, bool Json);
}
