using Aw.Packets.App.Features.ValidateLinePacket;
using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Model;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

/// <summary>PKT-SIZE-001 (LN-PKT3): each task against the sizes file, and unchecked without one.</summary>
public sealed class SizeChecksTests
{
    private const string SizesPath = "sizes.json";

    private static async Task<IReadOnlyList<CheckResult>> Check(PacketBuilder packet, SizesFile? sizes)
    {
        var source = new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet.Build()));
        var inspector = new PacketInspector(source, new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles));
        var sizeSource = new InMemorySizesSource();
        if (sizes is not null)
        {
            _ = sizeSource.With(SizesPath, SizesBuilder.Bytes(sizes));
        }

        var result = await new ValidatePacketService(inspector, sizeSource)
            .Handle(new ValidatePacketRequest("pkt", SizesFile: sizes is null ? null : SizesPath), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return [.. result.Value.Checks.Where(static c => string.Equals(c.Rule, PacketRules.SizeBudget, StringComparison.Ordinal))];
    }

    private static CheckResult Single(IReadOnlyList<CheckResult> checks) => Assert.Single(checks);

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_sizes_file_the_rule_is_unchecked_and_says_how_to_check_it()
    {
        var check = Single(await Check(A.Packet(), sizes: null));

        Assert.Equal(CheckState.Unchecked, check.State);
        Assert.Contains("--sizes", check.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_within_its_size_passes()
    {
        var check = Single(await Check(A.Packet(), A.Sizes()));

        Assert.Equal(CheckState.Pass, check.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_whose_write_scope_covers_more_files_than_its_size_allows_fails()
    {
        var packet = A.Packet().With(A.Task("t1").Sized(SizeClass.ExtraSmall).Writing("src/**", "docs/*.md", "README.md"));

        var check = Single(await Check(packet, A.Sizes()));

        Assert.Equal((CheckState.Fail, "task t1"), (check.State, check.Subject));
        Assert.Contains("4 files", check.Message, StringComparison.Ordinal);
        Assert.Contains("XS allows at most 3", check.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(check.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_path_declared_new_counts_as_a_file_and_a_file_two_globs_match_counts_once()
    {
        var sizes = A.Sizes() with { Sizes = A.Sizes().Sizes with { ExtraSmall = SizesBuilder.Limit(files: 3, items: 2, tokens: 50_000, turns: 20, wallMinutes: 30) } };
        var within = A.Packet().With(A.Task("t1").Sized(SizeClass.ExtraSmall).Budgeted(new TaskBudget()).Writing("src/**", "src/a.cs", "new: src/c.cs"));
        var over = A.Packet().With(A.Task("t1").Sized(SizeClass.ExtraSmall).Budgeted(new TaskBudget()).Writing("src/**", "new: src/c.cs", "new: src/d.cs"));

        Assert.Equal(CheckState.Pass, Single(await Check(within, sizes)).State);
        Assert.Equal(CheckState.Fail, Single(await Check(over, sizes)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_exactly_at_each_limit_of_its_size_passes()
    {
        var packet = A.Packet().With(A.Task("t1").Sized(SizeClass.ExtraSmall).Writing("src/**", "new: src/c.cs")
            .Example("e1", holdout: false, "test").Example("h1", holdout: true, "test")
            .Budgeted(new TaskBudget { Tokens = 50_000, Turns = 20, WallMinutes = 30 }));
        var sizes = A.Sizes() with { Sizes = A.Sizes().Sizes with { ExtraSmall = SizesBuilder.Limit(files: 3, items: 2, tokens: 50_000, turns: 20, wallMinutes: 30) } };

        var check = Single(await Check(packet, sizes));

        Assert.Equal(CheckState.Pass, check.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_task_with_more_acceptance_items_than_its_size_allows_fails_and_held_out_ones_count()
    {
        var packet = A.Packet().With(A.Task("t1").Sized(SizeClass.ExtraSmall).Example("e1", holdout: false, "test").Example("e2", holdout: false, "test").Example("h1", holdout: true, "test"));

        var check = Single(await Check(packet, A.Sizes()));

        Assert.Equal((CheckState.Fail, "task t1"), (check.State, check.Subject));
        Assert.Contains("3 acceptance items", check.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(200_000L, 10, 10, "200000 tokens")]
    [InlineData(1L, 61, 10, "61 turns")]
    [InlineData(1L, 10, 91, "91 wall minutes")]
    public async Task A_budget_over_the_sizes_limit_fails_naming_the_field(long tokens, int turns, int wall, string expected)
    {
        var packet = A.Packet().With(A.Task("t1").Budgeted(new TaskBudget { Tokens = tokens, Turns = turns, WallMinutes = wall }));

        var check = Single(await Check(packet, A.Sizes()));

        Assert.Equal(CheckState.Fail, check.State);
        Assert.Contains(expected, check.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_budget_field_a_task_leaves_out_and_a_limit_the_file_leaves_out_are_not_compared()
    {
        var open = A.Sizes() with { Sizes = A.Sizes().Sizes with { Small = new SizeLimit { Files = 8, Items = 4, Budget = new TaskBudget() } } };

        var unlimited = await Check(A.Packet().With(A.Task("t1").Budgeted(new TaskBudget { Tokens = 9_999_999_999L })), open);
        var silent = await Check(A.Packet().With(A.Task("t1").Budgeted(new TaskBudget())), A.Sizes());

        Assert.Equal(CheckState.Pass, Single(unlimited).State);
        Assert.Equal(CheckState.Pass, Single(silent).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_over_budget_task_is_reported_and_each_task_is_held_to_its_own_size()
    {
        var packet = A.Packet()
            .With(A.Task("t1").Sized(SizeClass.ExtraSmall).Writing("src/**", "docs/*.md", "README.md"))
            .With(A.Task("t2").Sized(SizeClass.Medium).Writing("src/**", "docs/*.md", "README.md").After("t1"))
            .With(A.Task("t3").Sized(SizeClass.Small).Example("e1", holdout: false, "test").Example("e2", holdout: false, "test").Example("e3", holdout: false, "test").Example("e4", holdout: false, "test").Example("h1", holdout: true, "test").After("t2"));

        var checks = await Check(packet, A.Sizes());

        Assert.Equal(["task t1", "task t3"], checks.Where(static c => c.State == CheckState.Fail).Select(static c => c.Subject), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task When_the_base_commit_cant_be_read_the_rule_is_unchecked_not_failed()
    {
        var packet = A.Packet().Base("0000000000000000000000000000000000000001");

        var check = Single(await Check(packet, A.Sizes()));

        Assert.Equal(CheckState.Unchecked, check.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_plan_graph_checks_its_tasks_against_the_sizes_and_the_other_line_kinds_have_no_such_rule()
    {
        var sizes = new InMemorySizesSource().With(SizesPath, SizesBuilder.Bytes(A.Sizes() with { Sizes = A.Sizes().Sizes with { Small = SizesBuilder.Limit(files: 1, items: 4, tokens: 150_000, turns: 60, wallMinutes: 90) } }));
        var repo = new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles);
        var plan = new ValidateLinePacketService(new LinePacketInspector(new InMemoryLinePacketSource().With("pkt", A.LinePacket(PacketKinds.Plan).Build()), repo, PacketFiles.Json), sizes, new InMemorySealSource());
        var review = new ValidateLinePacketService(new LinePacketInspector(new InMemoryLinePacketSource().With("pkt", A.LinePacket(PacketKinds.Review).Build()), repo, PacketFiles.Json), sizes, new InMemorySealSource());

        var planned = await plan.Handle(new ValidateLinePacketRequest("plan", "pkt", SizesFile: SizesPath), CancellationToken.None);
        var unsized = await plan.Handle(new ValidateLinePacketRequest("plan", "pkt"), CancellationToken.None);
        var reviewed = await review.Handle(new ValidateLinePacketRequest("review", "pkt"), CancellationToken.None);

        Assert.Contains(planned.Value.Checks, static c => string.Equals(c.Rule, PacketRules.SizeBudget, StringComparison.Ordinal) && c.State == CheckState.Fail);
        Assert.Contains(unsized.Value.Checks, static c => string.Equals(c.Rule, PacketRules.SizeBudget, StringComparison.Ordinal) && c.State == CheckState.Unchecked);
        Assert.DoesNotContain(reviewed.Value.Checks, static c => string.Equals(c.Rule, PacketRules.SizeBudget, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_sizes_file_that_cant_be_read_is_AW_PACKETS_009_not_a_check_result()
    {
        var inspector = new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(A.Packet().Build())),
            new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles));

        var result = await new ValidatePacketService(inspector, new InMemorySizesSource())
            .Handle(new ValidatePacketRequest("pkt", SizesFile: "nowhere.json"), CancellationToken.None);

        Assert.Equal(PacketsErrors.SizesUnusable.Code, result.Error.Code);
    }
}
