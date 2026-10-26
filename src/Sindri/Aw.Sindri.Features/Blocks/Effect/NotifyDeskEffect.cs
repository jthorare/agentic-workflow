using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
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
/// <param name="views">Reads the item's title for the banner; none (the default) leaves the banner untitled.</param>
internal sealed class NotifyDeskEffect(IDeskNotices notices, TimeProvider clock, IViewReader? views = null) : IEffectKind, IDescribedBlock
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
        cancellationToken.ThrowIfCancellationRequested();
        var address = InstanceAddress.From(context.Instance);
        var row = RowOf(context, address);
        var notice = new DeskNotice(context.Identity, address, ItemKey.From(context.Item),
            ClassOf(context), context.Text ?? Argument(context, "text") ?? context.Instance, clock.GetUtcNow(),
            row, row is { } id ? "needs-you/" + id.Value : null,   // switch to NeedsYouRoutes once it exists (API A2)
            context.FromEngine ? await TitleOf(context.Item, cancellationToken).ConfigureAwait(false) : null);
        var sent = await notices.Send(notice, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error;
        }

        return new EffectDone(context.FromEngine ? null : _delivered, Existed: !sent.Value);
    }

    /// <summary>The item's title, which an engine banner shows instead of an internal id; null when it can't be read (the banner then says just 'Sindri'; a notice is never failed for want of a title).</summary>
    private async ValueTask<string?> TitleOf(string item, CancellationToken cancellationToken)
    {
        if (views is null)
        {
            return null;
        }

        var alias = await views.Get<AliasRow>(ViewNames.ItemAliases, item, cancellationToken).ConfigureAwait(false);
        var id = alias is { IsError: false, Value: { } found } ? found.Item.Value : item;
        var row = await views.Get<ItemRow>(ViewNames.Items, id, cancellationToken).ConfigureAwait(false);
        return row is { IsError: false, Value.Title: { Length: > 0 } title } ? title : null;
    }

    /// <summary>An engine notice carries its class (§6.3); a recipe's notice is info (lane decision, P6 unit (c)).</summary>
    private static NoticeClass ClassOf(EffectContext context) =>
        context.FromEngine && string.Equals(Argument(context, "class"), "interrupt", StringComparison.Ordinal) ? NoticeClass.Interrupt : NoticeClass.Info;

    /// <summary>The needs-you row an engine notice is about, built only through <see cref="NeedsYouRowId"/>: a desk task's own row, a takeover's own row, and a park's or escalation's (a nested one too) on the root's row. A recipe's notice is about no row.</summary>
    private static NeedsYouRowId? RowOf(EffectContext context, InstanceAddress address) => !context.FromEngine ? null : Argument(context, "row") switch
    {
        "desk" => NeedsYouRowIds.Desk(address),
        "takeover" => NeedsYouRowIds.Takeover(address),
        "park" => NeedsYouRowIds.Park(InstanceAddress.From(InstanceSummaries.RootOf(address))),
        _ => null,
    };

    private static string? Argument(EffectContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
