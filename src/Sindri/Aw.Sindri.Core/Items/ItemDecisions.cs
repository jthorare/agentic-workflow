using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The pure decisions of the item commands (API spec §2.1 step 2).</summary>
public static class ItemDecisions
{
    /// <summary>Closes <paramref name="item"/> for <paramref name="reason"/> when it is still at the version the human saw: SND-ITEM-409 when it changed since, is already closed, or has an unfinished root instance, any of its roots (the human cancels that first).</summary>
    /// <param name="item">The item as the views hold it.</param>
    /// <param name="reason">Done or cancelled.</param>
    /// <param name="seenVersion">The version the human saw.</param>
    /// <param name="command">The 'CloseItem' command's id.</param>
    /// <returns>The state change to append, or SND-ITEM-409.</returns>
    public static Result<ItemStateChanged> Close(ItemRow item, CloseReason reason, long seenVersion, CommandId command)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!CloseReasons.TryTarget(reason, out var to))
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"{(int)reason} isn't a close reason (fix: done or cancelled)"));
        }

        if (EngineClosure.LiveRoot(item) is { } live)
        {
            return SindriErrors.ItemConflict.With($"{item.Id.Value} has an active instance {live} (fix: sindri workflow cancel {live})");   // the latest root may have ended and closed the item; an earlier one is still live
        }

        if (ItemStates.IsClosed(item.State))
        {
            return SindriErrors.ItemConflict.With($"{item.Id.Value} is already {ItemStateNames.Of(item.State)}");
        }

        if (item.Engine is { Ended: false } active)
        {
            return SindriErrors.ItemConflict.With($"{item.Id.Value} has an active instance {active.Address} (fix: sindri workflow cancel {active.Address})");
        }

        if (item.Version != seenVersion)
        {
            return SindriErrors.ItemConflict.With(string.Create(CultureInfo.InvariantCulture, $"{item.Id.Value} changed since you looked: it is at version {item.Version}, you saw {seenVersion}"));
        }

        return new ItemStateChanged(command, item.Id, item.State, to, item.Version + 1, Reason: null);
    }
}
