using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.open' (line spec §7.5, PB spec BD1): opens the phase's pull request from the frozen hand-off ('fact:handoff' names the packet directory holding pr.json and body.md;
/// 'fact:workspace' is the phase worktree, whose 'origin' names the repository), or updates the open one for the same head branch after a rework. It replies 'pr.opened' with
/// 'opened' or 'updated' and 'fact:pr' ({ link, number, state, base, head }); a forge that refuses the call replies 'refused', and any other forge failure fails the effect. The
/// pull request is found first; the head branch is then pushed (PushBranch, leased on the found pull request's head); a moved or rejected push replies 'refused'. The pull request is found again by its head branch and a marker
/// derived from it, so a retry or a second dispatch opens nothing twice.
/// </summary>
/// <param name="pulls">The forge.</param>
/// <param name="repos">Which repository the workspace belongs to.</param>
/// <param name="files">Reads pr.json and body.md of the hand-off.</param>
/// <param name="contents">Reads the stored facts.</param>
/// <param name="store">Stores 'fact:pr'.</param>
/// <param name="profile">The approved profile: 'workflow.ship.draft'.</param>
internal sealed class PrOpenEffect(IPullRequests pulls, IForgeRepos repos, IPacketFiles files, IFactContents contents, IFactStore store, IApprovedProfile profile)
    : IEffectKind, IDescribedBlock
{
    private static readonly OutcomeName _opened = OutcomeName.From("opened");
    private static readonly OutcomeName _updated = OutcomeName.From("updated");
    private static readonly OutcomeName _refused = OutcomeName.From("refused");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.pr.open"), BlockKind.Effect, Version: 1, Inputs: [FactKey.From("fact:handoff"), FactKey.From("fact:workspace")],
        Outputs: [new OutputSpec(FactKey.From("fact:pr"), [_opened, _updated])], Outcomes: [],
        Capability.Effects, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = await Plan(context, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var done = await Push(plan.Value, cancellationToken).ConfigureAwait(false);
        return done.IsError && string.Equals(done.Error.Code, SindriErrors.ForgeRefused.Code, StringComparison.Ordinal)
            ? new EffectDone(_refused, Existed: false)
            : done;
    }

    /// <summary>
    /// Finds the open pull request for the head branch first. With none, the branch is pushed with no lease (it must not exist yet) and a pull request opened; with one, the branch is
    /// pushed with a lease on that pull request's current head (a rework updates it) and the pull request updated. A moved or rejected push is a refusal.
    /// </summary>
    private async ValueTask<Result<EffectDone>> Push((OpenPull Request, string Workspace) plan, CancellationToken cancellationToken)
    {
        var (wanted, workspace) = plan;
        var found = await pulls.Find(new FindPull(wanted.Repo, wanted.Head, wanted.Marker), cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        string? lease = null;
        if (found.Value is { } existing)
        {
            var view = await pulls.Get(existing.Link, cancellationToken).ConfigureAwait(false);
            if (view.IsError)
            {
                return view.Error;
            }

            lease = view.Value.HeadSha;
        }

        var pushed = await pulls.PushBranch(new PullBranchPush(wanted.Repo, workspace, wanted.Head, wanted.HeadSha, lease), cancellationToken).ConfigureAwait(false);
        if (pushed.IsError)
        {
            return pushed.Error;
        }

        return pushed.Value == PullPushOutcome.Pushed
            ? await OpenOrUpdate(wanted, found.Value, cancellationToken).ConfigureAwait(false)
            : new EffectDone(_refused, Existed: false);
    }

    /// <summary>Updates the pull request found for the head, or opens one.</summary>
    private async ValueTask<Result<EffectDone>> OpenOrUpdate(OpenPull wanted, OpenedPull? existing, CancellationToken cancellationToken)
    {
        if (existing is not null)
        {
            // why: an update replaces the body whole, marker included, and the forge port appends the marker only on open.
            var updated = await pulls.Update(existing.Link, wanted.Title, wanted.Body.TrimEnd() + "\n\n<!-- aw-marker:" + wanted.Marker + " -->", cancellationToken).ConfigureAwait(false);
            return updated.IsError ? updated.Error : await Done(_updated, wanted, existing, cancellationToken).ConfigureAwait(false);
        }

        var opened = await pulls.Open(wanted, cancellationToken).ConfigureAwait(false);
        return opened.IsError ? opened.Error : await Done(_opened, wanted, opened.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The reply: the outcome and 'fact:pr' stored.</summary>
    private async ValueTask<Result<EffectDone>> Done(OutcomeName outcome, OpenPull wanted, OpenedPull pull, CancellationToken cancellationToken)
    {
        var fact = new JsonObject
        {
            ["link"] = pull.Link,
            ["number"] = pull.Number,
            ["state"] = "open",
            ["base"] = new JsonObject { ["ref"] = wanted.Base },
            ["head"] = new JsonObject { ["ref"] = wanted.Head, ["sha"] = wanted.HeadSha },
        };
        var stored = await store.Put(Encoding.UTF8.GetBytes(fact.ToJsonString()), cancellationToken).ConfigureAwait(false);
        return stored.IsError
            ? stored.Error
            : new EffectDone(outcome, Existed: false, new Dictionary<FactKey, ReplyFact> { [FactKey.From("fact:pr")] = new(stored.Value.Ref, stored.Value.Sha256, wanted.HeadSha) });
    }

    /// <summary>What to open: the hand-off's pr.json and body.md, the workspace's repository, and the profile's draft setting.</summary>
    private async ValueTask<Result<(OpenPull Request, string Workspace)>> Plan(EffectContext context, CancellationToken cancellationToken)
    {
        var handoff = await Fact(context, "fact:handoff", cancellationToken).ConfigureAwait(false);
        var workspace = await Fact(context, "fact:workspace", cancellationToken).ConfigureAwait(false);
        if (handoff.IsError || workspace.IsError)
        {
            return handoff.IsError ? handoff.Error : workspace.Error;
        }

        if (Parse(handoff.Value) is not { } packet || packet["dir"]?.GetValue<string>() is not { Length: > 0 } dir)
        {
            return Fail("fact:handoff names no packet directory (expected { hash, dir })");
        }

        var handoffFiles = await HandoffFiles(dir, cancellationToken).ConfigureAwait(false);
        if (handoffFiles.IsError)
        {
            return handoffFiles.Error;
        }

        var path = Path(workspace.Value);
        var repo = await repos.OfWorkspace(path, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var request = await Request(Parse(handoffFiles.Value.Pr), handoffFiles.Value.Body, repo.Value, cancellationToken).ConfigureAwait(false);
        return request.IsError ? request.Error : (request.Value, path);
    }

    private async ValueTask<Result<(string Pr, string Body)>> HandoffFiles(string dir, CancellationToken cancellationToken)
    {
        var pr = await files.Read(dir, "pr.json", cancellationToken).ConfigureAwait(false);
        if (pr.IsError)
        {
            return Fail($"the hand-off in {dir} has no readable pr.json: {pr.Error.Message}");
        }

        var body = await files.Read(dir, "body.md", cancellationToken).ConfigureAwait(false);
        return body.IsError
            ? Fail($"the hand-off in {dir} has no readable body.md: {body.Error.Message}")
            : (pr.Value, body.Value);
    }

    private async ValueTask<Result<OpenPull>> Request(JsonObject? pr, string body, PullRepo repo, CancellationToken cancellationToken)
    {
        if (Text(pr, "headBranch") is not { } head || Text(pr, "base") is not { } baseRef || Text(pr, "headSha") is not { } sha || Text(pr, "title") is not { } title)
        {
            return Fail("pr.json needs base, headBranch, headSha and title");
        }

        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        return approved.IsError
            ? approved.Error
            : new OpenPull(repo, head, baseRef, sha, title, body, approved.Value?.Profile.Workflow?.Ship?.Draft ?? false, Marker(head));
    }

    /// <summary>The marker a head branch's pull request carries: letters, digits, '.', '_', ':' and '-' only, so the same branch is found again.</summary>
    private static string Marker(string head)
    {
        var safe = new string([.. head.Select(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')]);
        return "sindri.pr:" + (safe.Length > 100 ? safe[..100] : safe);
    }

    /// <summary>The text of a stored fact in the context.</summary>
    private async ValueTask<Result<string>> Fact(EffectContext context, string key, CancellationToken cancellationToken)
    {
        if (!context.Facts.TryGetValue(FactKey.From(key), out var element) || StoredFact(element) is not { } fact)
        {
            return Fail($"{context.Instance} has no stored {key} for effect.pr.open");
        }

        var content = await contents.Read(fact, cancellationToken).ConfigureAwait(false);
        return content.IsError ? content.Error : Encoding.UTF8.GetString(content.Value.Span);
    }

    private static FactValue? StoredFact(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("ref", out var reference) && reference.ValueKind == JsonValueKind.String
        && element.TryGetProperty("sha256", out var sha) && sha.ValueKind == JsonValueKind.String
            ? new FactValue(reference.GetString()!, sha.GetString()!, HeadSha: null)   // why: both checked to be strings just above
            : null;

    /// <summary>The workspace path: a text fact holds it plain or as a JSON string.</summary>
    private static string Path(string text)
    {
        var trimmed = text.Trim();
        try
        {
            return trimmed.StartsWith('"') && JsonNode.Parse(trimmed) is JsonValue value && value.TryGetValue<string>(out var path) ? path : trimmed;
        }
        catch (JsonException)   // a quote that isn't a JSON string is part of the path
        {
            return trimmed;
        }
    }

    private static JsonObject? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)   // not JSON: the caller reports the missing fields
        {
            return null;
        }
    }

    private static string? Text(JsonObject? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static Error Fail(string message) => SindriErrors.InvalidInput.With("effect.pr.open: " + message);
}
