using Aw.Context.Core;
using Aw.Context.Core.WholeMap;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context map whole [--detail auto|nodes|areas] [--area KEY] [--locate KEY]... [--map-version V | --draft] [--repo R] [--json]' (Part D): a client of GET /sindri/context/map/whole. Exit 0 with an answer;
/// 1 for attention (not running, no map, no draft, a version not kept, a repo outside the scope, sessions off); 2 for usage or a tool error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class ContextWholeVerb(ControlPlaneClient client, Output output)
{
    private const string Form = "context map whole [--detail auto|nodes|areas] [--area <key>] [--locate <key>]... [--map-version <v> | --draft] [--repo <repo>] [--json]";

    private static readonly string[] _valued = ["--detail", "--area", "--map-version", "--repo"];
    private static readonly string[] _bare = ["--draft", "--json"];

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (Parse(args) is not { } o)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'context map whole {string.Join(' ', args)}' is not a whole-map command: {Form}")).ConfigureAwait(false);
        }

        if (o.Draft && o.MapVersion is not null)
        {
            return await Failed(ContextErrors.QueryInvalid.With("'--draft' and '--map-version' are two shapes of one question (fix: pass one of them, not both)")).ConfigureAwait(false);
        }

        var answer = await client.Get<WholeMapView>("/sindri/context/map/whole" + Query(o), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : o.Json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextWholeText.Whole(answer.Value.Value, client.SessionReason is null), 0).ConfigureAwait(false);
    }

    private static string Query(Options o)
    {
        var parts = new List<string>();
        if (o.Detail is not null)
        {
            parts.Add("detail=" + Uri.EscapeDataString(o.Detail));
        }

        if (o.Area is not null)
        {
            parts.Add("area=" + Uri.EscapeDataString(o.Area));
        }

        parts.AddRange(o.Locate.Select(static k => "locate=" + Uri.EscapeDataString(k)));
        if (o.Draft)
        {
            parts.Add("draft=true");
        }

        if (o.MapVersion is not null)
        {
            parts.Add("mapVersion=" + Uri.EscapeDataString(o.MapVersion));
        }

        if (o.Repo is not null)
        {
            parts.Add("repo=" + Uri.EscapeDataString(o.Repo));
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }

    /// <summary>The flags in any order; '--locate' repeats; null for a word, an unknown flag, a repeated single flag or a flag without its value.</summary>
    private static Options? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var locate = new List<string>();
        var bare = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var isLocate = string.Equals(arg, "--locate", StringComparison.Ordinal);
            var valued = isLocate || _valued.Contains(arg, StringComparer.Ordinal);
            if (valued && i + 1 >= args.Length)
            {
                return null;
            }

            if (isLocate)
            {
                locate.Add(args[++i]);
            }
            else if (valued ? !values.TryAdd(arg, args[++i]) : !_bare.Contains(arg, StringComparer.Ordinal) || !bare.Add(arg))
            {
                return null;
            }
        }

        return new Options(values.GetValueOrDefault("--detail"), values.GetValueOrDefault("--area"), locate, bare.Contains("--draft"), values.GetValueOrDefault("--map-version"), values.GetValueOrDefault("--repo"), bare.Contains("--json"));
    }

    private async Task<int> Failed(Kernel.Error error) => ContextExit.For(error, await output.Error(error).ConfigureAwait(false));

    private sealed record Options(string? Detail, string? Area, List<string> Locate, bool Draft, string? MapVersion, string? Repo, bool Json);
}
