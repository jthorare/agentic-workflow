using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Review;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The review half of 'sindri context map …' (context spec §8.7, README X63): review (the loop at a terminal, the queue, the
/// history, an export, single decisions), promote, propose and proposals. Every action is sent at once, from a human or a session (the typed
/// challenge on --apply, merge, add, delete and promote was removed per owner q93, 2026-10-23).
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="interactive">Whether stdin and stdout are a terminal.</param>
/// <param name="input">Where the typed answers are read.</param>
internal sealed class ContextReviewVerbs(ControlPlaneClient client, Output output, bool interactive, TextReader input)
{
    private const string Route = "/sindri/context/map";

    internal Task<int> Review(string[] args, CancellationToken ct)
    {
        var parsed = ReviewArguments.Parse(args);
        if (parsed.IsError)
        {
            return output.Error(parsed.Error);
        }

        var command = parsed.Value;
        return command.Mode switch
        {
            ReviewMode.Queue => Queue(command.Json, command.Limit, ct),
            ReviewMode.History => History(command.Json, command.Limit, ct),
            ReviewMode.Export => Export(command.Path, command.Json, ct),
            ReviewMode.Request => Send(command.Request!, command.Json, ct),   // why: the parser sets the request for this mode
            ReviewMode.Interactive => interactive ? Loop(ct) : Queue(json: false, limit: null, ct),
            _ => throw new UnreachableException($"review mode {command.Mode} has no handler"),
        };
    }

    internal async Task<int> Promote(CancellationToken ct)
    {
        var done = await client.Post<PromoteContextMapRequest, PromoteContextMapResponse>(Route + "/promote", new PromoteContextMapRequest(), ct).ConfigureAwait(false);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false) : await output.Text(ContextReviewText.Promote(done.Value.Value), 0).ConfigureAwait(false);
    }

    internal async Task<int> Propose(string file, bool json, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"can't read the proposals file {file}: {ex.Message} (fix: a JSON file with run, repo and surfaces)")).ConfigureAwait(false);
        }

        var read = output.Read<ProposeContextMapRequest>(bytes);
        if (read.IsError)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"can't read the proposals file {file}: {read.Error.Message} (fix: a JSON file with run, repo and surfaces)")).ConfigureAwait(false);
        }

        var request = read.Value;
        var answer = await client.Post<ProposeContextMapRequest, ProposeContextMapResponse>(Route + "/proposals", request, ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextReviewText.Proposed(answer.Value.Value, fence: client.SessionReason is null), 0).ConfigureAwait(false);
    }

    internal async Task<int> Proposals(string run, bool json, CancellationToken ct)
    {
        var answer = await client.Get<Context.Core.Proposals.ProposalsView>(Route + "/proposals?run=" + Uri.EscapeDataString(run), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextReviewText.Proposals(answer.Value.Value, fence: client.SessionReason is null), 0).ConfigureAwait(false);
    }

    private async Task<int> Queue(bool json, string? limit, CancellationToken ct)
    {
        var answer = await client.Get<ReviewQueueView>(Route + "/review" + LimitQuery(limit), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextReviewText.Queue(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private static string LimitQuery(string? limit) => limit is null ? string.Empty : "?limit=" + Uri.EscapeDataString(limit);

    private async Task<int> History(bool json, string? limit, CancellationToken ct)
    {
        var answer = await client.Get<ReviewHistoryView>(Route + "/review/history" + LimitQuery(limit), ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextReviewText.History(answer.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Export(string? path, bool json, CancellationToken ct)
    {
        var query = path is null ? string.Empty : "?path=" + Uri.EscapeDataString(path);
        var answer = await client.Get<ReviewExported>(Route + "/review/export" + query, ct).ConfigureAwait(false);
        return answer.IsError ? await Failed(answer.Error).ConfigureAwait(false)
            : json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextReviewText.Exported(answer.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>One review request, sent at once.</summary>
    private async Task<int> Send(ReviewContextMapRequest request, bool json, CancellationToken ct)
    {
        var done = await client.Post<ReviewContextMapRequest, ReviewContextMapResponse>(Route + "/review", request, ct).ConfigureAwait(false);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false)
            : json ? await output.Raw(done.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ContextReviewText.Review(done.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>The loop at a terminal: show each item, read a key, send what the session says; a failed action is shown and the loop goes on.</summary>
    private async Task<int> Loop(CancellationToken ct)
    {
        var queue = await client.Get<ReviewQueueView>(Route + "/review", ct).ConfigureAwait(false);
        if (queue.IsError)
        {
            return await Failed(queue.Error).ConfigureAwait(false);
        }

        if (queue.Value.Value.Entries.Count == 0)
        {
            return await output.Text(ContextReviewText.Queue(queue.Value.Value), 0).ConfigureAwait(false);
        }

        await output.Out.WriteAsync(string.Create(CultureInfo.InvariantCulture,
            $"draft {queue.Value.Value.Draft}: {queue.Value.Value.Undecided} undecided. Skipping writes nothing; quitting keeps what you decided.\n")).ConfigureAwait(false);
        var session = new ReviewSession(queue.Value.Value);
        var step = session.Start();
        var exit = 0;
        while (step.Prompt is not null)
        {
            await output.Out.WriteAsync(step.Prompt).ConfigureAwait(false);
            step = session.Answer(await input.ReadLineAsync(ct).ConfigureAwait(false) ?? "q");
            var (code, written) = await Apply(step, ct).ConfigureAwait(false);
            exit = Math.Max(exit, code);
            step = code == 0 ? session.Applied(step, written) : session.Failed(step);   // a count moves only when the server took the request
        }

        var c = session.Counts;
        await output.Out.WriteAsync(string.Create(CultureInfo.InvariantCulture,
            $"\naccepted {c.Accepted}, rejected {c.Rejected}, edited {c.Edited}, dropped {c.Dropped}, skipped {c.Skipped} (skipped items stay in the queue)\n")).ConfigureAwait(false);
        if (session.Hidden > 0)
        {
            await output.Out.WriteAsync(string.Create(CultureInfo.InvariantCulture,
                $"{session.Hidden} more undecided not shown here (the queue lists a limited number per group): run 'sindri context map review' again\n")).ConfigureAwait(false);
        }

        return exit;
    }

    // The exit code, and how many rows the server wrote (null when nothing was sent or it didn't say).
    private async Task<(int Code, int? Written)> Apply(ReviewStep step, CancellationToken ct)
    {
        if (step.Send is not { } request)
        {
            return (0, null);
        }

        var answer = await client.Post<ReviewContextMapRequest, ReviewContextMapResponse>(Route + "/review", request, ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return (await Failed(answer.Error).ConfigureAwait(false), null);
        }

        await output.Out.WriteAsync("\n" + ContextReviewText.Review(answer.Value.Value)).ConfigureAwait(false);
        return (0, answer.Value.Value.Result?.Written.Count);
    }

    private async Task<int> Failed(Kernel.Error error) => ContextExit.For(error, await output.Error(error).ConfigureAwait(false));
}
