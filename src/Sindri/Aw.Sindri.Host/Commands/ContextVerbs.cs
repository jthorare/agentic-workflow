using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri context …' (context spec §8.5, §8.6): clients of GET /sindri/context/query and /sindri/context/sources. Exit 0
/// when results came back (possibly degraded or empty); 1 for attention (not running, an unknown or out-of-scope
/// anchor, a refused question, session reads off); 2 for a tool error (an invalid profile, the store, every source
/// down) and usage errors. 'sources sync' is pending:S-5B, after API P6's jobs (SND-CLI-001, like every pending CLI form).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class ContextVerbs(ControlPlaneClient client, Output output)
{
    internal Task<int> Run(string[] args, CancellationToken ct) => args switch
    {
        ["query", .. var rest] => Query(rest, ct),
        ["sources", "status"] => Sources(json: false, ct),
        ["sources", "status", "--json"] => Sources(json: true, ct),
        ["sources", "sync"] => output.Error(SindriErrors.UnknownVerb.With(
            "'context sources sync' is pending:S-5B (a job: it ships with stage S-5B, after API P6's job runtime); repo docs refresh through the context-docs-refresh service: name it under services: in the profile")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'context {string.Join(' ', args)}' is not a context command: context query <text> | context show|neighbors|path | context eval probes | context sources status | context sources sync | context map import|export|status")),
    };

    private async Task<int> Query(string[] rest, CancellationToken ct)
    {
        var parsed = QueryArguments.Parse(rest);
        if (parsed.IsError)
        {
            return await Failed(parsed.Error).ConfigureAwait(false);   // a refused question (006) is attention (1); a usage error stays 2
        }

        var answer = await client.Get<ContextResult>("/sindri/context/query" + parsed.Value.QueryString, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        return parsed.Value.Json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextText.Render(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Sources(bool json, CancellationToken ct)
    {
        var answer = await client.Get<SourcesView>("/sindri/context/sources", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await Failed(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextText.Render(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Failed(Kernel.Error error)
    {
        var code = await output.Error(QueryFlagHints.Add(error)).ConfigureAwait(false);
        return error.Code == "SND-API-001" || ContextErrors.Attention.Contains(error.Code) ? 1 : code;
    }

    /// <summary>The query's arguments: the words of the question, then its flags; the query string they make.</summary>
    /// <param name="QueryString">'?q=…&amp;depth=…', every value escaped.</param>
    /// <param name="Json">True for --json.</param>
    internal sealed record QueryArguments(string QueryString, bool Json)
    {
        internal static Kernel.Result<QueryArguments> Parse(string[] args)
        {
            var words = new List<string>();
            var pairs = new List<(string Key, string Value)>();
            var json = false;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--json":
                        json = true;
                        break;
                    case "--draft":
                        pairs.Add(("draft", "true"));
                        break;
                    case "--node" or "--item" or "--symbol" or "--depth" or "--budget" when i + 1 < args.Length:
                        pairs.Add((args[i][2..], args[++i]));
                        break;
                    case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                        return SindriErrors.InvalidInput.With($"'{flag}' isn't a context query flag (or it needs a value): --node K | --item I | --symbol S | --depth N | --budget N | --draft | --json");
                    default:
                        words.Add(args[i]);
                        break;
                }
            }

            if (words.Count > 0)
            {
                pairs.Insert(0, ("q", string.Join(' ', words)));
            }

            var query = EncodedQuery(pairs);
            return query.IsError ? query.Error : new QueryArguments(query.Value, json);
        }

        private static Kernel.Result<string> EncodedQuery(List<(string Key, string Value)> pairs)
        {
            foreach (var (key, value) in pairs)
            {
                if (!QueryRules.FitsQueryUrl(value))
                {
                    return ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture,
                        $"{(key == "q" ? "the question" : "--" + key)} is over {QueryRules.MaxEncodedText} characters once percent-encoded for the query (shorten it)"));
                }
            }

            return UrlQuery.Of(pairs.Select(static p => (p.Key, (string?)p.Value)));
        }
    }
}
