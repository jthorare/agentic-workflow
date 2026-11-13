using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri agents [--provider P] [--state S] [--repo R] [--json]' and 'sindri agents show &lt;ref&gt; [--json]' (collector spec CI-8): clients of GET /sindri/sessions. They read the
/// routes and never start the control plane (API spec §7). Exit 0 ok (an empty list included); 1 when Sindri isn't running (SND-API-001) or the list carries a problem row; 2 on
/// usage or any other error (SND-SESSION-404 and SND-SESSION-002 included).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class AgentsVerbs(ControlPlaneClient client, Output output)
{
    internal const string Usage = "agents [--provider <agent>] [--state <s>] [--repo <r>] [--json] | agents show <provider>/<id> [--json] | " + AgentStartVerbs.StartUsage + " | " + AgentStartVerbs.RemoveUsage;

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var json = args.Contains("--json", StringComparer.Ordinal);
        var rest = args.Where(static a => a != "--json").ToArray();
        var starts = new AgentStartVerbs(client, output);
        return rest switch
        {
            ["start", ..] => await starts.Start(rest[1..], json, ct).ConfigureAwait(false),
            ["worktree", "remove", ..] => await starts.RemoveWorktree(rest[2..], json, ct).ConfigureAwait(false),
            ["show", ..] => await Show(rest[1..], json, ct).ConfigureAwait(false),
            _ => await List(rest, json, ct).ConfigureAwait(false),
        };
    }

    private async Task<int> List(string[] args, bool json, CancellationToken ct)
    {
        var query = Query(args);
        if (query.IsError)
        {
            return await output.Error(query.Error).ConfigureAwait(false);
        }

        var answer = await client.Get<SessionList>("/sindri/sessions" + query.Value, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var code = answer.Value.Value.Problems.Count > 0 ? 1 : 0;
        return json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(AgentsText.Render(answer.Value.Value), code).ConfigureAwait(false);
    }

    private async Task<int> Show(string[] args, bool json, CancellationToken ct)
    {
        if (args is not [{ } reference] || reference.Split('/', 2) is not [{ Length: > 0 } provider, { Length: > 0 } id])
        {
            return await output.Error(SindriErrors.InvalidInput.With("the one argument is the session: <provider>/<id>, for example claude-code/3970893f-… (" + Usage + ")")).ConfigureAwait(false);
        }

        var answer = await client.Get<SessionView>($"/sindri/sessions/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(id)}", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(AgentsText.Render(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private static Result<string> Query(string[] args)
    {
        var pairs = new List<(string, string?)>();
        for (var i = 0; i < args.Length; i += 2)
        {
            if (args[i] is not ("--provider" or "--state" or "--repo") || i + 1 >= args.Length)
            {
                return SindriErrors.InvalidInput.With($"'{args[i]}' isn't an argument of this command (or it needs a value): {Usage}");
            }

            pairs.Add((args[i][2..], args[i + 1]));
        }

        return UrlQuery.Of(pairs);
    }
}
