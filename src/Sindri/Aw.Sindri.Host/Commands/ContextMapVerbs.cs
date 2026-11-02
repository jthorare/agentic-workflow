using Aw.Context.Core.Map;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'context map import &lt;file&gt; | export [&lt;path&gt;] | status | review … | promote | propose | proposals | build' (context spec §8.5, §8.7;
/// the review half is <see cref="ContextReviewVerbs"/>). Import shows the file and its hash, then
/// sends the hash back; no typed phrase and no terminal are needed (owner decision q93).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="interactive">Whether stdin and stdout are a terminal.</param>
/// <param name="input">Where the review loop reads its keys.</param>
internal sealed class ContextMapVerbs(ControlPlaneClient client, Output output, bool interactive, TextReader input)
{
    private const string Route = "/sindri/context/map";

    private static readonly string[] _attention =
    [
        "SND-API-001", "SND-AUTH-002", "SND-CONTEXT-003", "SND-CONTEXT-005", "SND-CONTEXT-020", "SND-CONTEXT-030",
        "SND-CONTEXT-031", "SND-CONTEXT-032", "SND-CONTEXT-033", "SND-CONTEXT-034",
    ];

    private readonly ContextReviewVerbs _review = new(client, output, interactive, input);

    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["import", var file] => Import(Path.GetFullPath(file), ct),
        ["export", .. var rest] when Options(rest) is { } o => Export(o.Path, o.Json, ct),
        ["status", .. var rest] when StatusOptions(rest) is { } o => Status(o.Anchor, o.Json, ct),
        ["checks", .. var rest] when ChecksOptions(rest) is { } o => Checks(o.Source, o.Json, ct),
        ["review", .. var rest] => _review.Review(rest, ct),
        ["promote"] => _review.Promote(ct),
        ["propose", var file] => _review.Propose(file, json: false, ct),
        ["propose", var file, "--json"] => _review.Propose(file, json: true, ct),
        ["proposals", "--run", var run] => _review.Proposals(run, json: false, ct),
        ["proposals", "--run", var run, "--json"] => _review.Proposals(run, json: true, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With(
            $"'context map {string.Join(' ', args)}' is not a map command: context map import <file> | export [<path>] [--json] | status [--anchor <key>] [--json] | checks [--draft | --build] [--json] | review … | promote | propose <file> [--json] | proposals --run <id> [--json] | build [--detach] [--json]")),
    };

    private async Task<int> Import(string file, CancellationToken ct)
    {
        var shown = await client.Post<ImportContextMapRequest, ImportContextMapResponse>(Route + "/import", new(file, Hash: null, Answer: null), ct).ConfigureAwait(false);
        if (shown.IsError)
        {
            return await Failed(shown.Error).ConfigureAwait(false);
        }

        var preview = shown.Value.Value.Preview;
        await output.Out.WriteAsync(ContextMapText.Preview(preview)).ConfigureAwait(false);
        var done = await client.Post<ImportContextMapRequest, ImportContextMapResponse>(Route + "/import", new(file, preview.Hash, Answer: null), ct).ConfigureAwait(false);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false) : await output.Text(ContextMapText.Imported(done.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Export(string? path, bool json, CancellationToken ct)
    {
        var query = path is null ? string.Empty : "?path=" + Uri.EscapeDataString(path);
        var answer = await client.Get<MapExported>(Route + "/export" + query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(ContextMapText.Exported(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Status(string? anchor, bool json, CancellationToken ct)
    {
        var answer = await client.Get<MapStatusView>(Route + (anchor is null ? string.Empty : "?anchor=" + Uri.EscapeDataString(anchor)), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        // The publish half (code index spec 6.4) is a second read; when it fails the map's own status still prints.
        var publish = await client.Get<MapPublishStatusView>(Route + "/publish", ct).ConfigureAwait(false);
        if (json)
        {
            return await output.Raw(publish.IsError ? answer.Value.Body : ContextMapText.WithPublish(answer.Value.Body, publish.Value.Body), 0).ConfigureAwait(false);
        }

        return await output.Text(ContextMapText.Status(answer.Value.Value) + (publish.IsError ? string.Empty : ContextMapText.Publish(publish.Value.Value)), 0).ConfigureAwait(false);
    }

    // exit 0 all pass, 1 degraded, 2 usage or no map, 3 would be refused (map structure spec 4.3)
    private async Task<int> Checks(MapChecksSource source, bool json, CancellationToken ct)
    {
        var answer = await client.Post<CheckContextMapStructureRequest, MapChecksView>(Route + "/checks", new CheckContextMapStructureRequest(source), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        var exit = ContextMapText.ChecksExit(answer.Value.Value);
        return json ? await output.Raw(answer.Value.Body, exit).ConfigureAwait(false) : await output.Text(ContextMapText.Checks(answer.Value.Value), exit).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error)
    {
        var code = await output.Error(error).ConfigureAwait(false);
        return _attention.Contains(error.Code, StringComparer.Ordinal) ? 1 : ContextExit.For(error, code);
    }

    /// <summary>'[&lt;path&gt;] [--json]' in either order.</summary>
    private static (string? Path, bool Json)? Options(string[] rest) => rest switch
    {
        [] => (null, false),
        ["--json"] => (null, true),
        [var p] when !p.StartsWith("--", StringComparison.Ordinal) => (p, false),
        [var p, "--json"] when !p.StartsWith("--", StringComparison.Ordinal) => (p, true),
        ["--json", var p] when !p.StartsWith("--", StringComparison.Ordinal) => (p, true),
        _ => null,
    };

    /// <summary>'[--draft | --build] [--json]' in any order; the two source flags are exclusive.</summary>
    private static (MapChecksSource Source, bool Json)? ChecksOptions(string[] rest)
    {
        var json = rest.Contains("--json", StringComparer.Ordinal);
        var flags = rest.Where(static a => !string.Equals(a, "--json", StringComparison.Ordinal)).ToArray();
        return flags switch
        {
            [] => (MapChecksSource.Current, json),
            ["--draft"] => (MapChecksSource.Draft, json),
            ["--build"] => (MapChecksSource.Build, json),
            _ => null,
        };
    }

    /// <summary>'[--anchor &lt;key&gt;] [--json]' in either order.</summary>
    private static (string? Anchor, bool Json)? StatusOptions(string[] rest) => rest switch
    {
        [] => (null, false),
        ["--json"] => (null, true),
        ["--anchor", var a] when !a.StartsWith("--", StringComparison.Ordinal) => (a, false),
        ["--anchor", var a, "--json"] when !a.StartsWith("--", StringComparison.Ordinal) => (a, true),
        ["--json", "--anchor", var b] when !b.StartsWith("--", StringComparison.Ordinal) => (b, true),
        _ => null,
    };
}
