using System.Globalization;
using System.Text;
using Aw.Context.Core.Glossary;
using Aw.Kernel.Testing;
using static Aw.Context.Tests.Glossary.GlossaryFixtures;

namespace Aw.Context.Tests.Glossary;

public sealed class GlossaryGoldenTests
{
    // Golden: a six-feature fixture, rendered as text. AW_GOLDEN_UPDATE=1 rewrites it for review.
    [Fact, Trait("Lane", "unit")]
    public void The_six_feature_glossary_matches_its_golden_file()
    {
        List<FeatureVectors> features =
        [
            Feature("feature:billing.invoice", "Invoice totals", ("InvoiceTotal", [1f, 0.1f, 0f, 0f]), ("tax_rate_lookup", [0.9f, 0.1f, 0f, 0f])),
            Feature("feature:billing.refund", "Refund flow", ("RefundPayment", [1f, 0.2f, 0f, 0f]), ("tax_rate_refund", [0.9f, 0.2f, 0f, 0f])),
            Feature("feature:scheduling.shift", "Shift swap", ("ShiftSwap", [0f, 1f, 0.1f, 0f]), ("rosterPublish", [0.1f, 1f, 0f, 0f])),
            Feature("feature:scheduling.roster", "Roster publishing", ("RosterPublisher", [0f, 1f, 0.2f, 0f]), ("shift-board", [0f, 0.9f, 0.1f, 0f])),
            Feature("feature:search.index", "Search index", ("SearchIndexWriter", [0f, 0f, 1f, 0.1f])),
            Feature("feature:search.empty", "Nothing indexed yet"),
        ];

        Golden.Match(Render(Build(features)), typeof(GlossaryGoldenTests).Assembly, "Glossary/Golden/six-features.txt", Normalization.None);
    }

    private static string Render(MapGlossary glossary)
    {
        var text = new StringBuilder();
        foreach (var cluster in glossary.Clusters)
        {
            text.Append(CultureInfo.InvariantCulture, $"cluster {string.Join(' ', Keys(cluster.Features))} => {string.Join(' ', cluster.Terms)}\n");
        }

        foreach (var term in glossary.Terms)
        {
            text.Append(CultureInfo.InvariantCulture, $"term {term.Term} {term.Weight:F4} {string.Join(' ', Keys(term.Features))}\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"unvectored {string.Join(' ', Keys(glossary.Unvectored))}\ntruncated {glossary.Truncated}\n");
        return text.ToString();
    }
}
