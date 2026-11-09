using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.read-change', on the block runtime (scoping spec §4, §8.2): reads the PR or branch the scope input names from the item's repo checkout through the change port
/// (<see cref="IChangeSource"/>: the diff against the merge base at most 2 MiB with hunks under denied paths dropped, the commit messages and the PR body), scrubs its text, asks the
/// context engine's text shape for the anchors, then runs <c language="text">ScopingSteps.ReadChange</c>'s code. An item with no repo in the profile has no checkout to read from, so the Step fails.
/// </summary>
/// <param name="changes">The change port.</param>
/// <param name="checkouts">Where the repo's checkout is.</param>
/// <param name="shape">The engine's text shape.</param>
/// <param name="scrubber">Takes secrets out of the change's text.</param>
/// <param name="clock">When the change was read.</param>
internal sealed class ScopeReadChangeStep(IChangeSource changes, IRepoCheckouts checkouts, IScopingTextShape shape, ITextScrubber scrubber, TimeProvider clock) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.read-change");

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var input = Fact(context, "scopeInput");
        if (!string.Equals(ScopeJson.Str(input, "kind"), "change", StringComparison.Ordinal) || ScopeJson.Str(input, "ref") is not { Length: > 0 } reference)
        {
            return SindriErrors.InvalidInput.With("read-change reads a change: fact:scopeInput needs the kind 'change' and a ref (fix: use the reader for that kind, or name a PR number or a branch)");
        }

        if (context.Repo is not { } repo)
        {
            return SindriErrors.InvalidInput.With("the item has no repo, so there is no checkout to read the change from (fix: add the repo to the profile)");
        }

        var root = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var read = await changes.Read(root.Value, reference, ChangeRead.Scoping, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : await Source(context, read.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyDictionary<string, string>>> Source(StepContext context, ChangeRecord change, CancellationToken cancellationToken)
    {
        var text = scrubber.Scrub(ChangeBrief.Compose(change));
        var query = ContextQuery([]);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var asked = await shape.Anchors(context.Repo, text, cancellationToken).ConfigureAwait(false);
            if (asked.IsError)
            {
                return asked.Error;
            }

            query = ContextQuery(asked.Value);
        }

        var source = new JsonObject
        {
            ["key"] = change.Ref,
            ["title"] = scrubber.Scrub(ChangeBrief.Title(change)),
            ["text"] = text,
            ["version"] = $"{Short(change.Base)}..{Short(change.Head)}",
            ["fetchedAt"] = clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        };
        return Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["changeSource"] = ScopeJson.Write(source), ["contextQuery"] = query });
    }

    private static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;
}
