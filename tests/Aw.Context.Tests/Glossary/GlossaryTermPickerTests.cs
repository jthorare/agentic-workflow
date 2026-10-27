using Aw.Context.Core.Glossary;

namespace Aw.Context.Tests.Glossary;

public sealed class GlossaryTermPickerTests
{
    private static MapGlossary SharedLedger() => GlossaryFixtures.Build(
    [
        GlossaryFixtures.Feature("feature:a", "x", ("ledger", [1f, 0f, 0f]), ("audit", [1f, 0f, 0f])),
        GlossaryFixtures.Feature("feature:b", "x", ("ledger", [0f, 1f, 0f]), ("payout", [0f, 1f, 0f])),
        GlossaryFixtures.Feature("feature:c", "x", ("roster", [0f, 0f, 1f])),
    ]);

    [Fact, Trait("Lane", "unit")]
    public void The_term_the_most_of_the_features_carry_comes_first()
    {
        var top = GlossaryTermPicker.Top(SharedLedger(), ["feature:a", "feature:b"], take: 1);

        Assert.Equal(["ledger"], top, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_asked_features_terms_are_picked_and_the_count_is_capped()
    {
        var glossary = SharedLedger();

        var all = GlossaryTermPicker.Top(glossary, ["feature:c"], take: 10);
        var capped = GlossaryTermPicker.Top(glossary, ["feature:a", "feature:b"], take: 2);

        Assert.Equal(["roster"], all, StringComparer.Ordinal);
        Assert.Equal(2, capped.Count);
        Assert.DoesNotContain("roster", capped, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_glossary_and_no_features_give_no_terms()
    {
        Assert.Empty(GlossaryTermPicker.Top(glossary: null, ["feature:a"], take: 5));
        Assert.Empty(GlossaryTermPicker.Top(SharedLedger(), [], take: 5));
        Assert.Empty(GlossaryTermPicker.Top(SharedLedger(), ["feature:unknown"], take: 5));
    }
}
