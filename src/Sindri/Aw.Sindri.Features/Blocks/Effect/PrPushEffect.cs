using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.push' (PB spec §10.1, §6.1): pushes the workspace head to the PR head, with a lease on the head the snapshot saw ('fact:prState'), so a head
/// that moved is 'headMoved' and nothing is forced. It replies 'pr.pushed' with 'pushed', 'pushedSource' (a commit touches non-test source), 'nothing'
/// (the workspace is still at the seen head), 'headMoved' or 'rejected', and carries 'fact:push' ('{ sha, commits, comments, source, cause, ciFixes }'): every commit from the lease to the pushed head, oldest first, and 'ciFixes', the previous
/// 'fact:push' counter plus one when a push whose cause is 'ci' landed (what 'gate.pr.autofix-cap' counts). A dispatch
/// repeated after its push landed finds the branch at the commit and reads 'pushed' with <see cref="EffectDone.Existed"/>.
/// </summary>
/// <param name="pulls">The forge port.</param>
/// <param name="diff">Reads the workspace's head and changed paths.</param>
/// <param name="changes">Lists the commits a push carries.</param>
/// <param name="contents">Reads the facts it takes.</param>
/// <param name="facts">Stores the fact it replies with.</param>
internal sealed class PrPushEffect(IPullRequests pulls, IRunDiff diff, IBranchChanges changes, IFactContents contents, IFactStore facts) : IEffectKind, IDescribedBlock
{
    private static readonly FactKey _push = FactKey.From("fact:push");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Push;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var request = await ReadRequest(context, cancellationToken).ConfigureAwait(false);
        if (request.IsError)
        {
            return request.Error;
        }

        var head = await diff.Head(request.Value.Workspace, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        if (string.Equals(head.Value, request.Value.Lease, StringComparison.Ordinal))
        {
            return await Reply("nothing", new Pushed(request.Value, head.Value, [], Source: false, request.Value.CiFixes), existed: false, cancellationToken).ConfigureAwait(false);
        }

        var paths = await diff.ChangedPaths(request.Value.Workspace, request.Value.Lease, cancellationToken).ConfigureAwait(false);
        return paths.IsError
            ? paths.Error
            : await Push(request.Value, head.Value, paths.Value.Any(static p => !TestPaths.Is(p)), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<EffectDone>> Push(Request request, string head, bool source, CancellationToken cancellationToken)
    {
        var range = await changes.Between(request.Workspace, request.Lease, head, cancellationToken).ConfigureAwait(false);
        if (range.IsError)
        {
            return range.Error;
        }

        var sent = await pulls.Push(new PullPush(request.Link, request.Workspace, head, request.Lease), cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        var pushed = new Pushed(request, head, [.. range.Value.Commits.Select(static c => c.Sha)], source, request.CiFixes);
        return sent.Value switch
        {
            PullPushOutcome.Pushed => await Reply(PushedOutcome(source), pushed with { CiFixes = Counted(request, head, existed: false) }, existed: false, cancellationToken).ConfigureAwait(false),
            PullPushOutcome.HeadMoved => await AfterHeadMoved(pushed, cancellationToken).ConfigureAwait(false),
            PullPushOutcome.Rejected or _ => await Reply("rejected", pushed with { Commits = [] }, existed: false, cancellationToken).ConfigureAwait(false),
        };
    }

    private static string PushedOutcome(bool source) => source ? "pushedSource" : "pushed";

    private async ValueTask<Result<EffectDone>> AfterHeadMoved(Pushed pushed, CancellationToken cancellationToken)
    {
        var request = pushed.Request;
        var landed = await Landed(request.Link, pushed.Sha, cancellationToken).ConfigureAwait(false);   // why: a repeated dispatch finds its own push as a moved lease
        if (landed.IsError)
        {
            return landed.Error;
        }

        return landed.Value
            ? await Reply(PushedOutcome(pushed.Source), pushed with { CiFixes = Counted(request, pushed.Sha, existed: true) }, existed: true, cancellationToken).ConfigureAwait(false)
            : await Reply("headMoved", pushed with { Commits = [] }, existed: false, cancellationToken).ConfigureAwait(false);
    }

    // The counter after this dispatch: one more for a new ci push; a push found already made is counted once, so a retry that finds the earlier 'fact:push' (same sha) keeps it.
    private static int Counted(Request request, string head, bool existed) =>
        !string.Equals(request.Cause, "ci", StringComparison.Ordinal) || (existed && string.Equals(request.PreviousSha, head, StringComparison.Ordinal))
            ? request.CiFixes
            : request.CiFixes + 1;

    private async ValueTask<Result<bool>> Landed(string link, string head, CancellationToken cancellationToken)
    {
        var view = await pulls.Get(link, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : string.Equals(view.Value.HeadSha, head, StringComparison.Ordinal);
    }

    private async ValueTask<Result<EffectDone>> Reply(string outcome, Pushed pushed, bool existed, CancellationToken cancellationToken)
    {
        var commits = new JsonArray();
        foreach (var commit in pushed.Commits)
        {
            commits.Add((JsonNode)JsonValue.Create(commit));
        }

        var fact = new JsonObject
        {
            ["sha"] = pushed.Sha,
            ["commits"] = commits,
            ["comments"] = new JsonArray(),
            ["source"] = pushed.Source,
            ["cause"] = pushed.Request.Cause,
            ["ciFixes"] = pushed.CiFixes,
        };
        var stored = await facts.Put(Encoding.UTF8.GetBytes(fact.ToJsonString()), cancellationToken).ConfigureAwait(false);
        return stored.IsError
            ? stored.Error
            : new EffectDone(OutcomeName.From(outcome), existed, new Dictionary<FactKey, ReplyFact> { [_push] = new(stored.Value.Ref, stored.Value.Sha256, pushed.Sha) });
    }

    private async ValueTask<Result<Request>> ReadRequest(EffectContext context, CancellationToken cancellationToken)
    {
        var pr = await PrEffectFacts.Content(context, "pr", contents, cancellationToken).ConfigureAwait(false);
        var lease = pr.IsError ? pr.Error : await PrEffectFacts.Content(context, "lease", contents, cancellationToken).ConfigureAwait(false);
        var previous = await PrEffectFacts.Held(context, "fact:push", contents, cancellationToken).ConfigureAwait(false);
        var workspace = lease.IsError ? lease.Error : await PrEffectFacts.Content(context, "workspace", contents, cancellationToken).ConfigureAwait(false);
        if (workspace.IsError || previous.IsError)
        {
            return workspace.IsError ? workspace.Error : previous.Error;
        }

        return Build(context, pr.Value, lease.Value, workspace.Value, previous.Value);
    }

    private static string CauseOf(EffectContext context) =>
        context.Arguments.TryGetValue("cause", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString()! : "ci";   // why: checked to be a string just above

    private static Result<Request> Build(EffectContext context, JsonElement pr, JsonElement lease, JsonElement workspace, JsonElement? previous) =>
        PrEffectFacts.Link(pr) is { Length: > 0 } link && PrEffectFacts.Head(lease) is { Length: > 0 } seen && PrEffectFacts.Path(workspace) is { Length: > 0 } path
            ? new Request(link, seen, path, CauseOf(context), CiFixesOf(previous), ShaOf(previous))
            : PrEffectFacts.Missing(context, "fact:pr (a link), fact:prState (head.sha) and fact:workspace (a path)");

    /// <summary>The counter the previous 'fact:push' carried; 0 when the instance holds none.</summary>
    private static int CiFixesOf(JsonElement? previous) =>
        previous is { ValueKind: JsonValueKind.Object } fact && fact.TryGetProperty("ciFixes", out var count) && count.TryGetInt32(out var value) ? value : 0;

    private static string? ShaOf(JsonElement? previous) =>
        previous is { ValueKind: JsonValueKind.Object } fact && fact.TryGetProperty("sha", out var sha) && sha.ValueKind == JsonValueKind.String ? sha.GetString() : null;

    private sealed record Request(string Link, string Lease, string Workspace, string Cause, int CiFixes, string? PreviousSha);

    private sealed record Pushed(Request Request, string Sha, IReadOnlyList<string> Commits, bool Source, int CiFixes);
}
