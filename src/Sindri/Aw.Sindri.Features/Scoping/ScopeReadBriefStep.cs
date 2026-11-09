using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.read-brief', on the block runtime (scoping spec §7, §8.2): reads the brief file the scope input names from the item's repo checkout
/// (<see cref="IBriefFiles"/>: inside the repo, never a secret, never through a link that leaves it), asks the context engine's text shape for its anchors, then runs
/// <c language="text">ScopingSteps.ReadBrief</c>'s code. An item with no repo in the profile has no checkout to read from, so the Step fails and the instance parks.
/// </summary>
/// <param name="files">Reads the brief.</param>
/// <param name="checkouts">Where the repo's checkout is.</param>
/// <param name="shape">The engine's text shape.</param>
/// <param name="clock">When the file was read.</param>
internal sealed class ScopeReadBriefStep(IBriefFiles files, IRepoCheckouts checkouts, IScopingTextShape shape, TimeProvider clock) : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    private const string NoAnchors = """{"suggestedAnchors":[]}""";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.read-brief");

    /// <inheritdoc/>
    protected override async ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var input = Fact(context, "scopeInput");
        if (!string.Equals(ScopeJson.Str(input, "kind"), "brief", StringComparison.Ordinal) || BriefSource.PathOf(input) is not { } path)
        {
            return SindriErrors.InvalidInput.With("read-brief reads a brief: fact:scopeInput needs the kind 'brief' and a path (fix: use the reader for that kind, or name the file)");
        }

        if (context.Repo is not { } repo)
        {
            return SindriErrors.InvalidInput.With("the item has no repo, so there is no checkout to read the brief from (fix: add the repo to the profile)");
        }

        var root = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var file = await files.Read(path, [root.Value], cancellationToken).ConfigureAwait(false);
        if (file.IsError)
        {
            return file.Error;
        }

        var body = DeskBlocks.BriefBody(input, file.Value.Text);
        var anchors = string.IsNullOrWhiteSpace(body) ? NoAnchors : null;
        if (anchors is null)
        {
            var asked = await shape.Anchors(context.Repo, body, cancellationToken).ConfigureAwait(false);
            if (asked.IsError)
            {
                return asked.Error;
            }

            anchors = ContextQuery(asked.Value);
        }

        return Facts(new Dictionary<string, string>(StringComparer.Ordinal) { ["briefSource"] = BriefSource.Json(file.Value, clock.GetUtcNow()), ["contextQuery"] = anchors });
    }
}
