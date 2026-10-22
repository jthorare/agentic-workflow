using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.purge-marks' (PB spec §5.4, W26, IDEA-033): the babysitter's terminal cleanup. Deletes the comment marks of the pull request 'pr' names. It replies nothing.
/// </summary>
/// <param name="facts">Reads 'pr' back.</param>
/// <param name="marks">The comment marks (W26).</param>
internal sealed class PrPurgeMarksEffect(IFactContents facts, IPrCommentMarks marks) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.PurgeMarks;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var pr = await PrReplyFacts.Argument(facts, context, "pr", cancellationToken).ConfigureAwait(false);
        if (pr.IsError)
        {
            return pr.Error;
        }

        var target = Target(PrReplyFacts.Text(pr.Value, "link", "url", "pr"));
        if (target is null)
        {
            return WorkflowErrors.NoRuntime.With($"effect.pr.purge-marks of {context.Instance} needs the pull request's link ('pr': '.../owner/name/pull/N')");
        }

        // Whether another live instance names the PR is not asked: the instance reader lists by recipe, state and item, not by fact, so the ending babysitter's rows are always dropped.
        var purged = await marks.Purge(target, anotherInstanceNamesPr: false, cancellationToken).ConfigureAwait(false);
        return purged.IsError ? purged.Error : new EffectDone(Reply: null, Existed: purged.Value == 0);
    }

    private static PrRef? Target(string? link)
    {
        var parts = Uri.TryCreate(link, UriKind.Absolute, out var uri) ? uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) : [];
        return uri is not null && parts.Length >= 4 && parts[^2] == "pull" && int.TryParse(parts[^1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? new PrRef(uri.Host, $"{parts[^4]}/{parts[^3]}", number) : null;
    }
}
