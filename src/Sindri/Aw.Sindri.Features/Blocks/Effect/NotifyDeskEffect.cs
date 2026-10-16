using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.notify.desk' (engine spec §6.3, §12; README X5, X50; engine P6 unit (c)): a notice on the human's desk, one
/// line of 'notifications.jsonl' per effect identity and a macOS notification when one is available. A recipe's
/// replies 'notify.delivered' with 'delivered' and is info, carrying the entered state's rendered 'next' (the
/// interpreter's text); an engine notice carries its class and text in its arguments and replies nothing.
/// </summary>
/// <param name="notices">The notifications file and the OS notification centre.</param>
/// <param name="clock">When the notice is sent.</param>
internal sealed class NotifyDeskEffect(IDeskNotices notices, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.notify.desk"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Effects, Parameters: [], Rung: null, DeskCapable: false);

    private static readonly OutcomeName _delivered = OutcomeName.From("delivered");

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var notice = new DeskNotice(context.Identity, InstanceAddress.From(context.Instance), ItemKey.From(context.Item),
            ClassOf(context), context.Text ?? Argument(context, "text") ?? context.Instance, clock.GetUtcNow());
        var sent = await notices.Send(notice, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        return new EffectDone(context.FromEngine ? null : _delivered, Existed: !sent.Value);
    }

    /// <summary>An engine notice carries its class (§6.3); a recipe's notice is info (lane decision, P6 unit (c)).</summary>
    private static NoticeClass ClassOf(EffectContext context) =>
        context.FromEngine && string.Equals(Argument(context, "class"), "interrupt", StringComparison.Ordinal) ? NoticeClass.Interrupt : NoticeClass.Info;

    private static string? Argument(EffectContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
