using System.Globalization;
using Aw.Context.Core.Records;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    // Why a page can't be committed, or null. Checked before anything is opened, so a refused page touches nothing.
    private string? Problem(RecordBatch batch)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var write in batch.Records)
        {
            var record = write.Record;
            if (record.Source != batch.Source)
            {
                return $"record '{record.Key.Value}' belongs to source '{record.Source.Value}', not '{batch.Source.Value}'";
            }

            if (!keys.Add(record.Key.Value))
            {
                return $"record '{record.Key.Value}' is in the page twice";
            }

            if (SideProblem(write) is { } sideProblem)
            {
                return sideProblem;
            }

            if (record.Blocked && write.Chunks.Count > 0)
            {
                return $"record '{record.Key.Value}' is blocked and must have no chunks";
            }

            for (var i = 0; i < write.Chunks.Count; i++)
            {
                if (write.Chunks[i].Seq != i)
                {
                    return $"record '{record.Key.Value}' has chunk number {write.Chunks[i].Seq.ToString(CultureInfo.InvariantCulture)} where {i.ToString(CultureInfo.InvariantCulture)} was expected";
                }
            }
        }

        return batch.Removed.Any(k => keys.Contains(k.Value)) ? "a record is both written and removed in the page" : null;
    }
}
