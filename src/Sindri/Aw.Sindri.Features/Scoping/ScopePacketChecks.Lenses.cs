namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-LENS-001: lenses.json (from fact:lenses) agrees with the coverage rows, and its edges end at found nodes or anchors.</summary>
    private static void LensesMatchCoverage(ScopeContext c, RuleSink s)
    {
        if (!s.Need("lenses", "coverage"))
        {
            return;
        }

        foreach (var lens in ScopeRules.Lenses)
        {
            var entry = ScopeJson.Obj(c.Facts["lenses"], lens);
            var row = Rows(c).FirstOrDefault(r => string.Equals(ScopeJson.Str(r, "lens"), lens, StringComparison.Ordinal));
            if (entry is null || row is null)
            {
                s.Fail(lens, entry is null ? $"fact:lenses has no entry for the {lens} lens" : $"fact:coverage has no row for the {lens} lens", "Write one entry in fact:lenses and one row in fact:coverage for each lens.");
                continue;
            }

            foreach (var fault in LensShape.Faults(lens, entry, row))
            {
                s.Fail(lens, fault.Message, fault.Fix);
            }
        }
    }
}
