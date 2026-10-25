using System.Globalization;
using System.Text;
using Aw.Context.Core;
using Aw.Context.Core.Search;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context search &lt;words&gt; [--limit N] [--area KEY] [--repo R] [--map-version V | --draft] [--json]': a client of GET /sindri/context/search. The words are every argument that is not a flag.
/// Exit 0 with an answer (no hit is still an answer); 1 for attention (not running, no map, no draft, a version not kept, a repo outside the scope, sessions off); 2 for usage or a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class ContextSearchVerb(ControlPlaneClient client, Output output)
{
    private static readonly string[] _valued = ["--limit", "--area", "--repo", "--map-version"];
    private static readonly string[] _bare = ["--draft", "--json"];

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = Parse(args);
        if (parsed.IsError)
        {
            return ContextExit.For(parsed.Error, await output.Error(parsed.Error).ConfigureAwait(false));
        }

        var o = parsed.Value;
        var answer = await client.Get<MapSearchView>("/sindri/context/search" + Query(o), ct).ConfigureAwait(false);
        return answer.IsError ? ContextExit.For(answer.Error, await output.Error(answer.Error).ConfigureAwait(false))
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(Text(answer.Value.Value, o.Words, client.SessionReason is null), 0).ConfigureAwait(false);
    }

    internal static string Text(MapSearchView view, string words, bool fence)
    {
        var text = new StringBuilder(ContextLensText.Invariant($"search '{words}' in {view.MapVersion}: {view.Hits.Count} of {view.Total} {(view.Total == 1 ? "match" : "matches")}\n"));
        foreach (var hit in view.Hits)
        {
            _ = text.AppendLine(ContextLensText.Invariant($"  {hit.Key.Value}  {ContextLensText.Fenced(hit.Label, hit.Trust, fence)}  [{hit.Matched}]"));
            if (!string.Equals(hit.Snippet, hit.Label, StringComparison.Ordinal))
            {
                _ = text.Append("      ").AppendLine(ContextLensText.Fenced(hit.Snippet, hit.Trust, fence));
            }
        }

        return text.ToString();
    }

    private static string Query(Options o)
    {
        var parts = new List<string> { "q=" + Uri.EscapeDataString(o.Words) };
        if (o.Limit is { } limit)
        {
            parts.Add("limit=" + limit.ToString(CultureInfo.InvariantCulture));
        }

        if (o.Area is not null)
        {
            parts.Add("area=" + Uri.EscapeDataString(o.Area));
        }

        if (o.Repo is not null)
        {
            parts.Add("repo=" + Uri.EscapeDataString(o.Repo));
        }

        if (o.Draft)
        {
            parts.Add("draft=true");
        }

        if (o.MapVersion is not null)
        {
            parts.Add("mapVersion=" + Uri.EscapeDataString(o.MapVersion));
        }

        return "?" + string.Join('&', parts);
    }

    /// <summary>The flags in any order; a flag it doesn't know, a flag without its value, a limit that isn't a whole number, a repeated flag or no word at all is SND-CONTEXT-006.</summary>
    private static Kernel.Result<Options> Parse(string[] args)
    {
        var into = (Words: new List<string>(), Values: new Dictionary<string, string>(StringComparer.Ordinal), Bare: new HashSet<string>(StringComparer.Ordinal));
        for (var i = 0; i < args.Length; i++)
        {
            if (Take(args, ref i, into) is { } refused)
            {
                return refused;
            }
        }

        return Finish(into);
    }

    // One argument: a valued flag (with its value), a bare flag, an unknown flag (refused) or a word.
    private static Kernel.Error? Take(string[] args, ref int i, (List<string> Words, Dictionary<string, string> Values, HashSet<string> Bare) into)
    {
        var arg = args[i];
        if (_valued.Contains(arg, StringComparer.Ordinal))
        {
            return i + 1 >= args.Length || !into.Values.TryAdd(arg, args[++i]) ? Invalid($"'{arg}' needs one value") : null;
        }

        if (_bare.Contains(arg, StringComparer.Ordinal))
        {
            _ = into.Bare.Add(arg);
            return null;
        }

        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            return Invalid($"'{arg}' is not a search flag");
        }

        into.Words.Add(arg);
        return null;
    }

    private static Kernel.Result<Options> Finish((List<string> Words, Dictionary<string, string> Values, HashSet<string> Bare) into)
    {
        var limit = Limit(into.Values.GetValueOrDefault("--limit"));
        if (limit.IsError)
        {
            return limit.Error;
        }

        var draft = into.Bare.Contains("--draft");
        if (into.Words.Count == 0)
        {
            return Invalid("name the words to find (fix: sindri context search tab manager)");
        }

        return draft && into.Values.ContainsKey("--map-version")
            ? Invalid("'--draft' and '--map-version' are two shapes of one question (fix: pass one of them, not both)")
            : new Options(string.Join(' ', into.Words), limit.Value, into.Values.GetValueOrDefault("--area"), into.Values.GetValueOrDefault("--repo"), draft, into.Values.GetValueOrDefault("--map-version"), into.Bare.Contains("--json"));
    }

    private static Kernel.Result<int?> Limit(string? text)
    {
        if (text is null)
        {
            return (int?)null;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : Invalid($"'--limit' is a whole number from 1 to {MapSearchRules.MaxLimit}");
    }

    private static Kernel.Error Invalid(string reason) => ContextErrors.QueryInvalid.With(reason + " (fix: sindri context search <words> [--limit N] [--area KEY] [--repo R] [--map-version V | --draft] [--json])");

    private sealed record Options(string Words, int? Limit, string? Area, string? Repo, bool Draft, string? MapVersion, bool Json);
}
