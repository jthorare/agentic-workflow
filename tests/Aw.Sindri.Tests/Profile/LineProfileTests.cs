using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>
/// The line's profile keys (line spec LN-WF9, Appendix D): each one is typed, checked, shown in the approval diff, and a profile without them keeps its canonical JSON and its approval.
/// </summary>
public sealed class LineProfileTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(WorkflowSettings workflow) => _valid with { Workflow = workflow };

    private static WorkflowSettings Everything() => new(
        Packs: new PackSettings(
            new PackChoice("formal", new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "plain" }),
            new LensPackSettings(new PackChoice("design-two"), new PackChoice("code-review", new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "code-review" })),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["style/formal"] = "sha256:" + new string('a', 64) }),
        Line: new LineSettings(Parallel: 2, MaxOpenPrs: 4, MaxStack: 4),
        Plan: new PlanSettings(new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["S"] = new(8, 4, new BudgetSettings(400000, 60, 45)) }, "M"),
        Prove: new ProveSettings(2),
        Build: new BuildSettings(["dotnet build -warnaserror", "dotnet test"]),
        Ship: new ShipSettings(Draft: false),
        Design: new DesignSettings(new DesignModels("claude-writer", "codex-lens")),
        Review: new ReviewSettings(new ReviewModels("codex-lens")),
        Steps: new StepsSettings("2h"),
        Docs: new DocsSettings(["docs/**", "*.md"]));

    [Fact, Trait("Lane", "unit")]
    public void A_profile_with_every_line_key_is_accepted() =>
        Assert.True(ProfileRules.Check(With(Everything())).IsSuccess);

    private static readonly (string Key, SindriProfile Profile)[] _bad =
    [
        ("workflow.line.parallel", With(new(Line: new LineSettings(Parallel: 0)))),
        ("workflow.line.maxOpenPrs", With(new(Line: new LineSettings(MaxOpenPrs: 0)))),
        ("workflow.line.maxStack", With(new(Line: new LineSettings(MaxStack: -1)))),
        ("workflow.plan.maxPhaseSize", With(new(Plan: new PlanSettings(MaxPhaseSize: "L")))),
        ("workflow.plan.maxPhaseSize", With(new(Plan: new PlanSettings(MaxPhaseSize: "XXL")))),
        ("workflow.plan.sizes.XXL", With(new(Plan: new PlanSettings(new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["XXL"] = new(1, 1) })))),
        ("workflow.plan.sizes.S.files", With(new(Plan: new PlanSettings(new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["S"] = new(Files: -1) })))),
        ("workflow.plan.sizes.M.budget.tokens", With(new(Plan: new PlanSettings(new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["M"] = new(Budget: new BudgetSettings(Tokens: -5)) })))),
        ("workflow.prove.holdoutMin", With(new(Prove: new ProveSettings(0)))),
        ("workflow.build.commands", With(new(Build: new BuildSettings([])))),
        ("workflow.build.commands[1]", With(new(Build: new BuildSettings(["dotnet test", " "])))),
        ("workflow.build.commands[0]", With(new(Build: new BuildSettings(["dotnet test\nrm -rf /"])))),
        ("workflow.steps.longMax", With(new(Steps: new StepsSettings("2")))),
        ("workflow.docs.globs[0]", With(new(Docs: new DocsSettings(["/etc/**"])))),
        ("workflow.docs.globs[1]", With(new(Docs: new DocsSettings(["docs/**", "../secrets"])))),
        ("workflow.docs.globs", With(new(Docs: new DocsSettings([])))),
        ("workflow.packs.style.default", With(new(Packs: new PackSettings(Style: new PackChoice("Plain!"))))),
        ("workflow.packs.style.repos.web", With(new(Packs: new PackSettings(Style: new PackChoice(Repos: new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "" }))))),
        ("workflow.packs.lenses.design.default", With(new(Packs: new PackSettings(Lenses: new LensPackSettings(Design: new PackChoice("x")))))),
        ("workflow.packs.lenses.review.repos.web", With(new(Packs: new PackSettings(Lenses: new LensPackSettings(Review: new PackChoice(Repos: new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "NO" })))))),
        ("workflow.packs.pins.style", With(new(Packs: new PackSettings(Pins: new Dictionary<string, string>(StringComparer.Ordinal) { ["style"] = "sha256:" + new string('a', 64) })))),
        ("workflow.packs.pins.tone/plain", With(new(Packs: new PackSettings(Pins: new Dictionary<string, string>(StringComparer.Ordinal) { ["tone/plain"] = "sha256:" + new string('a', 64) })))),
        ("workflow.packs.pins.style/plain", With(new(Packs: new PackSettings(Pins: new Dictionary<string, string>(StringComparer.Ordinal) { ["style/plain"] = "abc" })))),
        ("workflow.design.models.writer", With(new(Design: new DesignSettings(new DesignModels(Writer: "Not An Agent"))))),
        ("workflow.design.models.lens", With(new(Design: new DesignSettings(new DesignModels(Lens: "x"))))),
        ("workflow.review.models.lens", With(new(Review: new ReviewSettings(new ReviewModels(Lens: "-bad"))))),
    ];

    public static TheoryData<int> Cases() => [.. Enumerable.Range(0, _bad.Length)];


    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void A_bad_value_is_refused_naming_its_key_path(int index)
    {
        var (key, profile) = _bad[index];
        var error = ProfileRules.Check(profile).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith(key + ":", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_line_cannot_run_more_phases_at_once_than_the_box_has_heavy_slots()
    {
        var parallel = With(new(Line: new LineSettings(Parallel: 3)));

        var error = ProfileRules.Check(parallel, new BoxLimits(2)).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith("workflow.line.parallel:", error.Message, StringComparison.Ordinal);
        Assert.Contains("heavy-slot cap of 2", error.Message, StringComparison.Ordinal);
        Assert.Contains("heavy-slots-cap", error.Message, StringComparison.Ordinal);
        Assert.True(ProfileRules.Check(parallel, new BoxLimits(3)).IsSuccess);
        Assert.True(ProfileRules.Check(parallel).IsSuccess);   // without the box, the profile alone is checked
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parallel_left_out_is_not_held_to_a_cap_below_its_default()
    {
        Assert.True(ProfileRules.Check(With(new(Line: new LineSettings(MaxStack: 3))), new BoxLimits(1)).IsSuccess);
        Assert.True(ProfileRules.Check(_valid, new BoxLimits(1)).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_each_line_key_that_changes()
    {
        var live = ProfileRules.Check(_valid).Value;

        var changed = ProfileDiff.Between(live, With(Everything()));

        Assert.Equal(
            [
                "workflow.build.commands", "workflow.design.models.lens", "workflow.design.models.writer", "workflow.docs.globs", "workflow.line.maxOpenPrs", "workflow.line.maxStack",
                "workflow.line.parallel", "workflow.packs.lenses.design.default", "workflow.packs.lenses.review.default", "workflow.packs.lenses.review.repos.web", "workflow.packs.pins.style/formal",
                "workflow.packs.style.default", "workflow.packs.style.repos.web", "workflow.plan.maxPhaseSize", "workflow.plan.sizes.S.budget.tokens", "workflow.plan.sizes.S.budget.turns",
                "workflow.plan.sizes.S.budget.wallMinutes", "workflow.plan.sizes.S.files", "workflow.plan.sizes.S.items", "workflow.prove.holdoutMin", "workflow.review.models.lens",
                "workflow.ship.draft", "workflow.steps.longMax",
            ],
            changed,
            StringComparer.Ordinal);
        Assert.Equal(["workflow.line.parallel"], ProfileDiff.Between(With(new(Line: new LineSettings(2))), With(new(Line: new LineSettings(3)))), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_keys_keeps_its_canonical_json_and_hash()
    {
        var before = ProfileRules.Check(With(new("1m"))).Value;
        var json = Encoding.UTF8.GetString(AwJson.Serialize(before, SliceHarness.Json));

        foreach (var key in new[] { "packs", "line", "plan", "prove", "build", "ship", "design", "review", "steps", "docs" })
        {
            Assert.DoesNotContain($"\"{key}\"", json, StringComparison.Ordinal);
        }

        Assert.NotEqual(
            ProfileHashing.Of(AwJson.Serialize(before, SliceHarness.Json)),
            ProfileHashing.Of(AwJson.Serialize(before with { Workflow = new("1m", Line: new LineSettings(3)) }, SliceHarness.Json)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_key_left_out_means_its_default()
    {
        Assert.Equal((2, 4, 4, "M", 2, "2h", false), (LineProfile.Parallel(_valid), LineProfile.MaxOpenPrs(_valid), LineProfile.MaxStack(_valid), LineProfile.MaxPhaseSize(_valid), LineProfile.HoldoutMin(_valid), LineProfile.LongMax(_valid), LineProfile.Draft(_valid)));
        Assert.Equal(["docs/**", "*.md"], LineProfile.DocsGlobs(_valid), StringComparer.Ordinal);
        Assert.Equal((5, 3, 7), (LineProfile.Parallel(With(new(Line: new LineSettings(5)))), LineProfile.MaxStack(With(new(Line: new LineSettings(MaxStack: 3)))), LineProfile.HoldoutMin(With(new(Prove: new ProveSettings(7))))));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_sizes_file_has_the_five_classes_with_the_defaults_and_the_profiles_overrides_merged_in()
    {
        var sizes = JsonNode.Parse(LineProfile.SizesFile(With(Everything())))!;

        Assert.Equal(1, (int)sizes["schemaVersion"]!);
        Assert.Equal(["XS", "S", "M", "L", "XL"], sizes["sizes"]!.AsObject().Select(p => p.Key), StringComparer.Ordinal);
        Assert.Equal((3, 2), ((int)sizes["sizes"]!["XS"]!["files"]!, (int)sizes["sizes"]!["XS"]!["items"]!));
        Assert.Equal((8, 4, 400000L), ((int)sizes["sizes"]!["S"]!["files"]!, (int)sizes["sizes"]!["S"]!["items"]!, (long)sizes["sizes"]!["S"]!["budget"]!["tokens"]!));
        Assert.Equal((20, 8), ((int)sizes["sizes"]!["M"]!["files"]!, (int)sizes["sizes"]!["M"]!["items"]!));
    }

    [Fact, Trait("Lane", "unit")]
    public void One_size_overridden_leaves_the_others_at_their_defaults()
    {
        var sizes = JsonNode.Parse(LineProfile.SizesFile(With(new(Plan: new PlanSettings(new Dictionary<string, SizeSettings>(StringComparer.Ordinal) { ["M"] = new(Files: 30) })))))!;

        Assert.Equal((30, 8), ((int)sizes["sizes"]!["M"]!["files"]!, (int)sizes["sizes"]!["M"]!["items"]!));
        Assert.Equal((8, 4), ((int)sizes["sizes"]!["S"]!["files"]!, (int)sizes["sizes"]!["S"]!["items"]!));
    }
}
