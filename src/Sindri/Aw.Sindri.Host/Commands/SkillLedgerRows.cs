using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ReadLedger;

namespace Aw.Sindri.Host.Commands;

/// <summary>The ledger read 'sindri skill run --follow' makes of the trial's item.</summary>
internal static class SkillLedgerRows
{
    /// <summary>
    /// Every ledger row of the item's type from <paramref name="after"/> on, oldest first: one page after another until the read says there is no more ('next' absent), however many pages came back empty
    /// (a page scans a fixed span of the ledger, so an empty one can still have a 'next'); null when a page can't be read.
    /// </summary>
    internal static async Task<List<PublicEvent>?> Read(ControlPlaneClient client, string item, string type, long after, CancellationToken ct)
    {
        var rows = new List<PublicEvent>();
        var cursor = after;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var page = await client.Get<LedgerPage>($"/sindri/ledger?item={Uri.EscapeDataString(item)}&type={type}&after={cursor.ToString(CultureInfo.InvariantCulture)}", ct).ConfigureAwait(false);
            if (page.IsError)
            {
                return null;
            }

            rows.AddRange(page.Value.Value.Events);
            if (page.Value.Value.Next is not { } next || next <= cursor)   // why: a 'next' that doesn't move would loop forever
            {
                return rows;
            }

            cursor = next;
        }
    }
}
