using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri request', 'requests', 'decide' and 'needs-you' (API P4 unit H): clients of the request, decision and needs-you routes. Human
/// verbs start the control plane unless --no-start; inside an agent session they never do (API spec §7). 'request' is a session's verb; 'decide'
/// is the human's (the API refuses a session, SND-AUTH-002). 'requests', 'decisions' and 'needs-you' exit 1 while the views rebuild or stall, so a script
/// doesn't read a partial list as the truth (API spec §3.6); --json prints the route's body as received.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class RequestVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = RequestVerbArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = ensureRunning is null ? null : await ensureRunning(!command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        return command.Verb switch
        {
            RequestVerbArguments.Verb.Request => await File(command, ct).ConfigureAwait(false),
            RequestVerbArguments.Verb.Requests => await List(command, ct).ConfigureAwait(false),
            RequestVerbArguments.Verb.Decisions => await Decisions(command, ct).ConfigureAwait(false),
            RequestVerbArguments.Verb.Decide => await Decide(command, ct).ConfigureAwait(false),
            RequestVerbArguments.Verb.NeedsYou => await NeedsYou(command, ct).ConfigureAwait(false),
            RequestVerbArguments.Verb.None => await output.Error(SindriErrors.UnknownVerb.With(RequestVerbArguments.Usage)).ConfigureAwait(false),
            _ => await output.Error(SindriErrors.UnknownVerb.With(RequestVerbArguments.Usage)).ConfigureAwait(false),
        };
    }

    private async Task<int> File(RequestVerbArguments.Parsed command, CancellationToken ct)
    {
        _ = ItemRef.TryParse(command.Item, out var item);   // the arguments already checked it
        var body = new FileRequestRequest(command.Kind, command.Text, command.Item is null ? null : item);
        var filed = await client.Post<FileRequestRequest, RequestFiledResponse>("/sindri/requests", body, ct).ConfigureAwait(false);
        if (filed.IsError)
        {
            return await output.Error(filed.Error).ConfigureAwait(false);
        }

        var response = filed.Value.Value;
        return command.Json
            ? await output.Raw(filed.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{response.RequestId.Value} filed; {response.DecisionId.Value} waits for a human (sequence {response.Accepted.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private async Task<int> List(RequestVerbArguments.Parsed command, CancellationToken ct)
    {
        var query = UrlQuery.Of([("state", command.State), ("item", command.Item)]);

        var answer = await client.GetView<ListRequestsResponse>("/sindri/requests" + query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var list = answer.Value.Value;
        var code = list.AsOf.Rebuilding || list.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RequestText.Requests(list), code).ConfigureAwait(false);
    }

    private async Task<int> Decisions(RequestVerbArguments.Parsed command, CancellationToken ct)
    {
        var pending = command.Pending is { } p ? (p ? "true" : "false") : null;
        var query = UrlQuery.Of([("pending", pending), ("item", command.Item)]);

        var answer = await client.GetView<ListDecisionsResponse>("/sindri/decisions" + query, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var list = answer.Value.Value;
        var code = list.AsOf.Rebuilding || list.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RequestText.Decisions(list), code).ConfigureAwait(false);
    }

    private async Task<int> Decide(RequestVerbArguments.Parsed command, CancellationToken ct)
    {
        var (decision, choice) = (command.Decision!.Value, command.Choice!.Value);   // why: Parse sets both for 'decide'
        var decided = await client.Post<DecideBody, CommandAccepted>("/sindri/decisions/" + decision.Value, new DecideBody(choice, command.Note), ct).ConfigureAwait(false);
        if (decided.IsError)
        {
            return await output.Error(decided.Error).ConfigureAwait(false);
        }

        return command.Json
            ? await output.Raw(decided.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(string.Create(CultureInfo.InvariantCulture, $"{decision.Value} decided: {choice.Value} (sequence {decided.Value.Value.Sequence})\n"), 0).ConfigureAwait(false);
    }

    private async Task<int> NeedsYou(RequestVerbArguments.Parsed command, CancellationToken ct)
    {
        var answer = await client.GetView<GetNeedsYouResponse>("/sindri/needs-you", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var list = answer.Value.Value;
        var code = list.AsOf.Rebuilding || list.AsOf.Stalled ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RequestText.NeedsYou(list), code).ConfigureAwait(false);
    }
}
