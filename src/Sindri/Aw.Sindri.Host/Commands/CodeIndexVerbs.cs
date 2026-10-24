using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Sindri.Core;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri code-index status|symbols|callers|callees' (code-index spec §10, CI-10a): clients of GET /sindri/code-index/…, read only, never starting the control plane.
/// A session's answer comes fenced from the server, so the text is printed as it is. Exit 0 with an answer (for status, only when every repo is ok); 1 for attention (a repo not ok, the control plane down, the not-found answers SND-CODEIDX-011 and -013, so
/// a repo out of scope looks the same as one never built); 2 for a tool error (including SND-CODEIDX-012).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class CodeIndexVerbs(ControlPlaneClient client, Output output)
{
    private const string Form = "code-index status [--repo R] [--json] | symbols <query> [--repo R] [--prefix] [--json] | callers|callees <ref> [--depth 1..8] [--repo R] [--json]";

    private static readonly string[] _attention = [CodeIndexErrors.IndexNotBuilt.Code, CodeIndexErrors.SymbolNotFound.Code, "SND-API-001", "SND-AUTH-002"];

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        if (Parse(args) is not { } o)
        {
            return await output.Error(SindriErrors.UnknownVerb.With($"'code-index {string.Join(' ', args)}' is not a code-index command: {Form}")).ConfigureAwait(false);
        }

        return o.Verb switch
        {
            "status" => await Status(o, ct).ConfigureAwait(false),
            "symbols" => await Symbols(o, ct).ConfigureAwait(false),
            _ => await Calls(o, ct).ConfigureAwait(false),
        };
    }

    private async Task<int> Status(Options o, CancellationToken ct)
    {
        var status = await client.Get<CodeIndexStatusView>("/sindri/code-index/status" + Query(("repo", o.Repo)), ct).ConfigureAwait(false);
        return status.IsError ? await Failed(status.Error).ConfigureAwait(false)
            : await Answer(status.Value.Body, CodeIndexText.Status(status.Value.Value), o.Json, status.Value.Value.Repos.All(static r => r.State == IndexState.Ok) ? 0 : 1).ConfigureAwait(false);
    }

    private async Task<int> Symbols(Options o, CancellationToken ct)
    {
        var symbols = await client.Get<FindSymbolsAnswer>("/sindri/code-index/symbols" + Query(("query", o.Subject), ("repo", o.Repo), ("match", o.Prefix ? "prefix" : null)), ct).ConfigureAwait(false);
        return symbols.IsError ? await Failed(symbols.Error).ConfigureAwait(false)
            : await Answer(symbols.Value.Body, CodeIndexText.Symbols(symbols.Value.Value), o.Json, 0).ConfigureAwait(false);
    }

    private async Task<int> Calls(Options o, CancellationToken ct)
    {
        var calls = await client.Get<CodeCallsView>("/sindri/code-index/" + o.Verb + Query(("ref", o.Subject), ("depth", o.Depth), ("repo", o.Repo)), ct).ConfigureAwait(false);
        return calls.IsError ? await Failed(calls.Error).ConfigureAwait(false)
            : await Answer(calls.Value.Body, CodeIndexText.Calls(calls.Value.Value, o.Verb), o.Json, 0).ConfigureAwait(false);
    }

    private async Task<int> Answer(ReadOnlyMemory<byte> body, string text, bool json, int exit) => json ? await output.Raw(body, exit).ConfigureAwait(false) : await output.Text(text, exit).ConfigureAwait(false);

    private async Task<int> Failed(Kernel.Error error)
    {
        var toolExit = await output.Error(error).ConfigureAwait(false);
        return _attention.Contains(error.Code, StringComparer.Ordinal) ? 1 : toolExit;
    }

    private static string Query(params (string Name, string? Value)[] pairs)
    {
        var parts = pairs.Where(static p => p.Value is not null).Select(static p => p.Name + "=" + Uri.EscapeDataString(p.Value ?? string.Empty)).ToList();
        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }

    private sealed record Options(string Verb, string? Subject, string? Repo, string? Depth, bool Prefix, bool Json);

    private static readonly string[] _verbs = ["status", "symbols", "callers", "callees"];

    /// <summary>The flags a verb takes: the ones that carry a value, then the switches.</summary>
    private static (string[] Valued, string[] Switches) Flags(string verb) => verb switch
    {
        "status" => (["--repo"], ["--json"]),
        "symbols" => (["--repo"], ["--json", "--prefix"]),
        _ => (["--repo", "--depth"], ["--json"]),
    };

    /// <summary>A verb, its one subject (symbols, callers and callees need one, status takes none) and its flags in any order; null for a bad shape, an unknown or repeated flag, or one missing its value.</summary>
    private static Options? Parse(string[] args)
    {
        if (args is not [var verb, .. var rest] || !_verbs.Contains(verb, StringComparer.Ordinal))
        {
            return null;
        }

        if (Scan(rest, Flags(verb)) is not var (subject, flags))
        {
            return null;
        }

        return string.Equals(verb, "status", StringComparison.Ordinal) != (subject is null)
            ? null
            : new Options(verb, subject, flags.GetValueOrDefault("--repo"), flags.GetValueOrDefault("--depth"), flags.ContainsKey("--prefix"), flags.ContainsKey("--json"));
    }

    private static (string? Subject, Dictionary<string, string> Flags)? Scan(string[] rest, (string[] Valued, string[] Switches) allowed)
    {
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        string? subject = null;
        for (var i = 0; i < rest.Length; i++)
        {
            var arg = rest[i];
            var valued = allowed.Valued.Contains(arg, StringComparer.Ordinal);
            if (!arg.StartsWith("--", StringComparison.Ordinal) && subject is null)
            {
                subject = arg;
            }
            else if (!arg.StartsWith("--", StringComparison.Ordinal) || flags.ContainsKey(arg) || !(valued ? i + 1 < rest.Length : allowed.Switches.Contains(arg, StringComparer.Ordinal)))
            {
                return null;
            }
            else
            {
                flags[arg] = valued ? rest[++i] : string.Empty;
            }
        }

        return (subject, flags);
    }
}
