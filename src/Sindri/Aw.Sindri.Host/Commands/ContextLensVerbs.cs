using Aw.Context.Core;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context lenses &lt;anchor&gt;... [--depth N] [--hops H] [--limit N] [--offset N] [--draft | --map-version V] [--json]', 'sindri context history &lt;anchor&gt;... [--since T] [--before T] [--draft | --map-version V] [--json]' and 'sindri context filemap --repo R [--path P] [--unmapped] [--draft] [--json]'
/// (context spec §8.5, §8.9): clients of GET /sindri/context/lenses and /filemap. Exit 0 with an answer; 1 for attention (not running,
/// unknown anchor, no draft, a repo outside the scope, sessions off); 2 for a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class ContextLensVerbs(ControlPlaneClient client, Output output)
{
    private const string Forms = "context lenses <anchor>... [--depth <0-4>] [--hops <state,flow,dependency,component>] [--limit <n>] [--offset <n>] [--draft | --map-version <v>] [--json] | context history <anchor>... [--since <t>] [--before <t>] [--draft | --map-version <v>] [--json] | context filemap --repo <repo> [--path <dir>] [--unmapped] [--draft] [--cursor <c>] [--limit <n>] [--json] | context filemap accept|reject <link-id>";

    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["lenses", .. var rest] when Lenses(rest) is { } l => Lenses(l, ct),
        ["history", .. var rest] when History(rest) is { } h => History(h, ct),
        ["filemap", .. var rest] when FileMap(rest) is { } f => FileMap(f, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'context {string.Join(' ', args)}' is not a lenses, history or filemap command: {Forms}")),
    };

    private async Task<int> Lenses((string[] Anchors, string? Depth, string? Hops, bool Draft, string? MapVersion, string? Limit, string? Offset, bool Json) o, CancellationToken ct) =>
        o.Draft && o.MapVersion is not null
            ? await Failed(ContextErrors.QueryInvalid.With("'--draft' and '--map-version' are two shapes of one question (fix: pass one of them, not both)")).ConfigureAwait(false)
            : await Read(o, ct).ConfigureAwait(false);

    private async Task<int> Read((string[] Anchors, string? Depth, string? Hops, bool Draft, string? MapVersion, string? Limit, string? Offset, bool Json) o, CancellationToken ct)
    {
        var query = LensQuery(o);
        if (o.Anchors.Distinct(StringComparer.Ordinal).Skip(1).Any())
        {
            var set = await client.GetOrQuery<GetContextLensesAnswerRequest, LensesSetView>("/sindri/context/lenses", query, Body(o), ct).ConfigureAwait(false);
            return set.IsError ? await Failed(set.Error).ConfigureAwait(false)
                : o.Json ? await output.Raw(set.Value.Body, 0).ConfigureAwait(false)
                : await output.Text(ContextLensText.LensesSet(set.Value.Value, client.SessionReason is null), 0).ConfigureAwait(false);
        }

        var answer = await client.GetOrQuery<GetContextLensesAnswerRequest, LensesView>("/sindri/context/lenses", query, Body(o), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextLensText.Lenses(answer.Value.Value, client.SessionReason is null), 0).ConfigureAwait(false);
    }

    /// <summary>The lenses question as the GET query: each anchor, then each option that was given.</summary>
    private static string LensQuery((string[] Anchors, string? Depth, string? Hops, bool Draft, string? MapVersion, string? Limit, string? Offset, bool Json) o)
    {
        var query = "?" + string.Join('&', o.Anchors.Select(static a => "anchor=" + Uri.EscapeDataString(a)));
        (string Name, string? Value)[] given = [("depth", o.Depth), ("hops", o.Hops), ("limit", o.Limit), ("offset", o.Offset)];
        foreach (var (name, value) in given)
        {
            if (value is not null)
            {
                query += "&" + name + "=" + Uri.EscapeDataString(value);
            }
        }

        return query + (o.Draft ? "&draft=true" : string.Empty) + (o.MapVersion is null ? string.Empty : "&mapVersion=" + Uri.EscapeDataString(o.MapVersion));
    }

    /// <summary>The lenses question as the QUERY body; a depth that is no number is left null here, as the GET's own binding refuses it.</summary>
    private static GetContextLensesAnswerRequest Body((string[] Anchors, string? Depth, string? Hops, bool Draft, string? MapVersion, string? Limit, string? Offset, bool Json) o) =>
        new(o.Anchors, o.Draft, Number(o.Depth), o.MapVersion, o.Hops, Number(o.Limit), Number(o.Offset));

    // A value that is no number is left null here, as the GET's own binding refuses it.
    private static int? Number(string? text) =>
        int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;

    private async Task<int> History((string[] Anchors, string? Since, string? Before, bool Draft, string? MapVersion, bool Json) o, CancellationToken ct)
    {
        if (o.Draft && o.MapVersion is not null)
        {
            return await Failed(ContextErrors.QueryInvalid.With("'--draft' and '--map-version' are two shapes of one question (fix: pass one of them, not both)")).ConfigureAwait(false);
        }

        var query = "?" + string.Join('&', o.Anchors.Select(static a => "anchor=" + Uri.EscapeDataString(a))) + (o.Since is null ? string.Empty : "&since=" + Uri.EscapeDataString(o.Since))
            + (o.Before is null ? string.Empty : "&before=" + Uri.EscapeDataString(o.Before)) + (o.Draft ? "&draft=true" : string.Empty) + (o.MapVersion is null ? string.Empty : "&mapVersion=" + Uri.EscapeDataString(o.MapVersion));
        var answer = await client.GetOrQuery<GetContextHistoryRequest, HistoryView>("/sindri/context/history", query, new GetContextHistoryRequest(o.Anchors, o.Since, o.Before, o.Draft, o.MapVersion), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextHistoryText.History(answer.Value.Value, client.SessionReason is null), 0).ConfigureAwait(false);
    }

    private async Task<int> FileMap((string Repo, string? Path, bool Unmapped, bool Draft, bool Json, string? Cursor, string? Limit) o, CancellationToken ct)
    {
        var query = "?repo=" + Uri.EscapeDataString(o.Repo) + (o.Path is null ? string.Empty : "&path=" + Uri.EscapeDataString(o.Path))
            + (o.Unmapped ? "&unmapped=true" : string.Empty) + (o.Draft ? "&draft=true" : string.Empty)
            + (o.Cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(o.Cursor)) + (o.Limit is null ? string.Empty : "&limit=" + Uri.EscapeDataString(o.Limit));
        var answer = await client.Get<FileMapView>("/sindri/context/filemap" + query, ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextLensText.FileMap(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error) => ContextExit.For(error, await output.Error(error).ConfigureAwait(false));

    /// <summary>One or more anchors with [--depth N], [--draft or --map-version V] and [--json] in any order; null for none; a draft with a version parses and is refused as 006 by the command.</summary>
    private static (string[] Anchors, string? Depth, string? Hops, bool Draft, string? MapVersion, string? Limit, string? Offset, bool Json)? Lenses(string[] rest)
    {
        var flags = Flags(rest, valued: ["--depth", "--hops", "--map-version", "--limit", "--offset"], bare: ["--draft", "--json"]);
        var version = flags?.Values.GetValueOrDefault("--map-version");
        return flags is { Words.Length: > 0 }
            ? (flags.Words, flags.Values.GetValueOrDefault("--depth"), flags.Values.GetValueOrDefault("--hops"), flags.Bare.Contains("--draft"), version, flags.Values.GetValueOrDefault("--limit"), flags.Values.GetValueOrDefault("--offset"), flags.Bare.Contains("--json"))
            : null;
    }

    /// <summary>One or more anchors with [--since T] [--before T], [--draft or --map-version V] and [--json] in any order; null for none.</summary>
    private static (string[] Anchors, string? Since, string? Before, bool Draft, string? MapVersion, bool Json)? History(string[] rest)
    {
        var flags = Flags(rest, valued: ["--since", "--before", "--map-version"], bare: ["--draft", "--json"]);
        return flags is { Words.Length: > 0 }
            ? (flags.Words, flags.Values.GetValueOrDefault("--since"), flags.Values.GetValueOrDefault("--before"), flags.Bare.Contains("--draft"), flags.Values.GetValueOrDefault("--map-version"), flags.Bare.Contains("--json"))
            : null;
    }

    /// <summary>'--repo R' with [--path P] [--unmapped] [--draft] [--cursor C] [--limit N] [--json] in any order.</summary>
    private static (string Repo, string? Path, bool Unmapped, bool Draft, bool Json, string? Cursor, string? Limit)? FileMap(string[] rest)
    {
        var flags = Flags(rest, valued: ["--repo", "--path", "--cursor", "--limit"], bare: ["--unmapped", "--draft", "--json"]);
        return flags is { Words: [], Values: var v } && v.TryGetValue("--repo", out var repo)
            ? (repo, v.GetValueOrDefault("--path"), flags.Bare.Contains("--unmapped"), flags.Bare.Contains("--draft"), flags.Bare.Contains("--json"), v.GetValueOrDefault("--cursor"), v.GetValueOrDefault("--limit"))
            : null;
    }

    /// <summary>Sorts the arguments into words, valued flags and bare flags; null for an unknown flag, a repeated one or one without its value.</summary>
    private static ParsedFlags? Flags(string[] args, string[] valued, string[] bare)
    {
        var words = new List<string>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (valued.Contains(arg, StringComparer.Ordinal))
            {
                if (i + 1 >= args.Length || !values.TryAdd(arg, args[++i]))
                {
                    return null;
                }
            }
            else if (bare.Contains(arg, StringComparer.Ordinal))
            {
                _ = set.Add(arg);
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }
            else
            {
                words.Add(arg);
            }
        }

        return new ParsedFlags([.. words], values, set);
    }

    private sealed record ParsedFlags(string[] Words, Dictionary<string, string> Values, HashSet<string> Bare);
}
