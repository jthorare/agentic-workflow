using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>
/// The needs-you tag hashes each row as the client receives it (#2789 review): any field the client sees changes the tag, so a 304
/// never hides a change, while a timeout's countdown alone keeps it, so a list with a timeout row can still answer 304.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouTagTests
{
    private static readonly ViewsStatus _current = new(Cursor: 10, LedgerHead: 10, ProjectedAt: KnownInstants.Epoch, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null);

    private static string Tag(params NeedsYouRow[] rows) => GetNeedsYouService.TagOf(rows, empty: null, _current, SliceHarness.Json);

    [Fact]
    public void Two_rows_that_differ_only_in_their_notice_get_different_tags()
    {
        var row = A.NeedsYouRow().Build();

        Assert.NotEqual(Tag(row with { Notice = "Recorded; Sindri acts on it next." }), Tag(row with { Notice = "Recorded; waiting for the run to end." }), StringComparer.Ordinal);
    }

    [Fact]
    public void A_decision_whose_option_label_changes_gets_a_different_tag()
    {
        var row = A.NeedsYouRow().Build();
        var accept = new NeedsYouDecisionOption(DecisionOption.From("accept"), "Accept", "sindri decide DEC-1 accept", RejectLike: false);
        var decision = new NeedsYouDecision(DecisionId.From("DEC-00000000000000000000000000000001"), "Split it?", "session:demo", accept.Id, [accept]);

        Assert.NotEqual(
            Tag(row with { Decision = decision }),
            Tag(row with { Decision = decision with { Options = [accept with { Label = "Accept the split" }] } }),
            StringComparer.Ordinal);
    }

    [Fact]
    public void A_timeout_countdown_alone_keeps_the_tag_but_a_new_target_changes_it()
    {
        var row = A.NeedsYouRow().Build() with { Timeout = new StatusTimeout(TimeoutKind.Parks, InSeconds: 300, On: "review") };

        Assert.Equal(Tag(row), Tag(row with { Timeout = row.Timeout with { InSeconds = 299 } }));
        Assert.NotEqual(Tag(row), Tag(row with { Timeout = row.Timeout with { On = "approval" } }), StringComparer.Ordinal);
    }
}
