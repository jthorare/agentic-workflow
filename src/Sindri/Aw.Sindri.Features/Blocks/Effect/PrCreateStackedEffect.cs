using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.create-stacked' (docs-sync spec §7.2, PB spec §10.1): opens the documentation pull request on top of the code pull request. 'pr' is the code pull request's link, 'on' its
/// snapshot ('head.ref' and 'head.sha'), 'workspace' the worktree holding the docs commit. The docs branch is 'docs/{code head branch}'. The pull request is found first by that branch and a
/// marker naming the code pull request and the instance; the branch is then pushed (no lease when there is none yet, else leased on the found pull request's head), and the pull request opened
/// against the code head branch, or updated. It replies 'pr.stacked' with 'opened' or 'updated' and 'fact:docsPr' (the docs pull request's link); a push that doesn't land, or a forge that
/// refuses the call, replies 'rejected'.
/// </summary>
/// <param name="pulls">The forge.</param>
/// <param name="diff">Reads the workspace's head.</param>
/// <param name="contents">Reads the stored facts.</param>
/// <param name="store">Stores 'fact:docsPr'.</param>
internal sealed class PrCreateStackedEffect(IPullRequests pulls, IRunDiff diff, IFactContents contents, IFactStore store)
    : IEffectKind, IDescribedBlock
{
    private static readonly OutcomeName _rejected = OutcomeName.From("rejected");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.CreateStacked;

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

        var done = await Stack(plan.Value, cancellationToken).ConfigureAwait(false);
        return done.IsError && string.Equals(done.Error.Code, SindriErrors.ForgeRefused.Code, StringComparison.Ordinal)
            ? new EffectDone(_rejected, Existed: false)
            : done;
    }

    /// <summary>Finds the docs pull request, pushes the docs branch (leased on the found one's head), and updates or opens.</summary>
    private async ValueTask<Result<EffectDone>> Stack(StackPlan plan, CancellationToken cancellationToken)
    {
        var wanted = plan.Request;
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

        var pushed = await pulls.PushBranch(new PullBranchPush(wanted.Repo, plan.Workspace, wanted.Head, wanted.HeadSha, lease), cancellationToken).ConfigureAwait(false);
        if (pushed.IsError)
        {
            return pushed.Error;
        }

        return pushed.Value == PullPushOutcome.Pushed
            ? await OpenOrUpdate(wanted, found.Value, cancellationToken).ConfigureAwait(false)
            : new EffectDone(_rejected, Existed: false);
    }

    private async ValueTask<Result<EffectDone>> OpenOrUpdate(OpenPull wanted, OpenedPull? existing, CancellationToken cancellationToken)
    {
        if (existing is not null)
        {
            // why: an update replaces the body whole, marker included, and the forge port appends the marker only on open.
            var updated = await pulls.Update(existing.Link, wanted.Title, wanted.Body.TrimEnd() + "\n\n<!-- aw-marker:" + wanted.Marker + " -->", cancellationToken).ConfigureAwait(false);
            return updated.IsError ? updated.Error : await Done("updated", existing.Link, wanted.HeadSha, cancellationToken).ConfigureAwait(false);
        }

        var opened = await pulls.Open(wanted, cancellationToken).ConfigureAwait(false);
        return opened.IsError ? opened.Error : await Done("opened", opened.Value.Link, wanted.HeadSha, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The reply: the outcome, and 'fact:docsPr' (the docs pull request's link) stored.</summary>
    private async ValueTask<Result<EffectDone>> Done(string outcome, string link, string headSha, CancellationToken cancellationToken)
    {
        var stored = await store.Put(Encoding.UTF8.GetBytes(link), cancellationToken).ConfigureAwait(false);
        return stored.IsError
            ? stored.Error
            : new EffectDone(OutcomeName.From(outcome), Existed: false, new Dictionary<FactKey, ReplyFact> { [FactKey.From("fact:docsPr")] = new(stored.Value.Ref, stored.Value.Sha256, headSha) });
    }

    /// <summary>What to open: the code pull request's link and snapshot, the workspace's head and the title.</summary>
    private async ValueTask<Result<StackPlan>> Plan(EffectContext context, CancellationToken cancellationToken)
    {
        var link = await Argument(context, "pr", cancellationToken).ConfigureAwait(false);
        var snapshot = await Argument(context, "on", cancellationToken).ConfigureAwait(false);
        var workspace = await Argument(context, "workspace", cancellationToken).ConfigureAwait(false);
        if (link.IsError || snapshot.IsError || workspace.IsError)
        {
            return link.IsError ? link.Error : snapshot.IsError ? snapshot.Error : workspace.Error;
        }

        var source = Source(PullLink(link.Value), Parse(snapshot.Value));
        if (source.IsError)
        {
            return source.Error;
        }

        if (!context.Arguments.TryGetValue("title", out var title) || title.ValueKind != JsonValueKind.String || title.GetString() is not { Length: > 0 } text)
        {
            return Fail("needs a title");
        }

        var path = workspace.Value.Trim();
        var head = await diff.Head(path, cancellationToken).ConfigureAwait(false);
        return head.IsError ? head.Error : new StackPlan(Request(source.Value, context.Instance, (head.Value, text)), path);
    }

    private static OpenPull Request(SourcePull source, string instance, (string Head, string Title) docs)
    {
        var marker = Marker(instance, source.Number);
        var body = $"Documentation sync for {source.Link}.\n\nSynced head: {source.HeadSha}\n";
        return new OpenPull(source.Repo, "docs/" + source.HeadBranch, source.HeadBranch, docs.Head, docs.Title, body, Draft: false, marker);
    }

    /// <summary>The marker naming the code pull request and the instance: letters, digits, '.', '_', ':' and '-' only, so the docs pull request is found again.</summary>
    private static string Marker(string instance, int number)
    {
        var safe = new string([.. instance.Select(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '-')]);
        return "sindri.docs:" + safe + ":" + number.ToString(CultureInfo.InvariantCulture);
    }

    private static Result<SourcePull> Source((PullRepo Repo, int Number, string Link)? link, JsonObject? snapshot)
    {
        if (link is not { } pull)
        {
            return Fail("'pr' is not a pull request link (https://host/owner/name/pull/number)");
        }

        var head = snapshot?["head"] as JsonObject;
        return Text(head, "ref") is { } branch && Text(head, "sha") is { } sha
            ? new SourcePull(pull.Repo, pull.Number, pull.Link, branch, sha)
            : Fail("'on' has no head.ref and head.sha");
    }

    /// <summary>The repository and number a pull request link names.</summary>
    private static (PullRepo Repo, int Number, string Link)? PullLink(string text)
    {
        var link = text.Trim().Trim('"');   // a text fact holds the link plain or as a JSON string
        return Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) is [var owner, var name, "pull", var number]
            && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                ? (new PullRepo(uri.IdnHost, owner, name), n, link)
                : null;
    }

    /// <summary>The text of a stored fact passed as an argument.</summary>
    private async ValueTask<Result<string>> Argument(EffectContext context, string name, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue(name, out var element) || StoredFact(element) is not { } fact)
        {
            return Fail($"{context.Instance} has no stored '{name}' argument");
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

    private static Error Fail(string message) => SindriErrors.InvalidInput.With("effect.pr.create-stacked: " + message);

    private sealed record SourcePull(PullRepo Repo, int Number, string Link, string HeadBranch, string HeadSha);

    private sealed record StackPlan(OpenPull Request, string Workspace);
}
