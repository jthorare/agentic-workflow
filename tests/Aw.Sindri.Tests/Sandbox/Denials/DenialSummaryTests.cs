using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The one-line summary of a launch's denials that an idle node's failure text carries (#3565).</summary>
public sealed class DenialSummaryTests
{
    private static readonly DateTimeOffset _at = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static DenialEntry Entry(string target, int count) => new(DenialKind.Policy, target, DenialRules.InnerPolicy, count, _at, _at);

    private static DenialFile Counter(IReadOnlyList<DenialEntry> entries, IReadOnlyList<DenialOverflow>? overflow = null) =>
        new(LaunchId.From("LCH-0123456789abcdef0123456789abcdef"), _at, entries, overflow ?? []);

    [Fact, Trait("Lane", "unit")]
    public void No_denials_give_no_line()
    {
        Assert.Null(DenialSummary.Line([]));
        Assert.Null(DenialSummary.Line([Counter([])]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repeated_target_shows_its_count_and_the_most_refused_comes_first()
    {
        var line = DenialSummary.Line([Counter([Entry("Bash: gh auth", 1), Entry("Bash: gh run", 3)])]);

        Assert.Equal("the sandbox refused 4 actions (Bash: gh run x3, Bash: gh auth)", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void One_denial_reads_as_one_action() => Assert.Equal("the sandbox refused 1 action (Bash: gh run)", DenialSummary.Line([Counter([Entry("Bash: gh run", 1)])]));

    [Fact, Trait("Lane", "unit")]
    public void The_same_target_in_two_files_adds_up()
    {
        var line = DenialSummary.Line([Counter([Entry("Bash: gh run", 2)]), Counter([Entry("Bash: gh run", 1)])]);

        Assert.Equal("the sandbox refused 3 actions (Bash: gh run x3)", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void Past_three_targets_the_rest_count_as_more()
    {
        var line = DenialSummary.Line([Counter([Entry("a", 5), Entry("b", 4), Entry("c", 3), Entry("d", 2), Entry("e", 1)])]);

        Assert.Equal("the sandbox refused 15 actions (a x5, b x4, c x3, +2 more)", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void Overflow_counts_in_the_total_and_adds_one_more_per_kind()
    {
        var line = DenialSummary.Line([Counter([Entry("a", 1)], [new DenialOverflow(DenialKind.Network, 7)])]);

        Assert.Equal("the sandbox refused 8 actions (a, +1 more)", line);
    }
}
