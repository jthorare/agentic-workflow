using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetProfile;

/// <summary>GET /profile (API spec §3.1): the 'profile' view's one row.</summary>
public sealed class GetProfileTests
{
    private static readonly ProfileHash _a = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _b = ProfileHash.From(new string('b', 64));
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetProfile_shows_approved_and_live_hash_and_the_changed_paths()
    {
        var approvedAt = _v.Slice.Clock.GetUtcNow();
        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(_a, []));
        _v.Slice.Clock.Advance(TimeSpan.FromSeconds(30));
        await _v.Fold(EventTypes.ProfileChecked, new ProfileChecked(_v.Slice.Ids.New<CommandId>(), _a, _b, Drifted: true,
            ["services.heartbeat.every"], [ServiceName.From("heartbeat")]));

        var result = await Service().Handle(A.GetProfileRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var profile = result.Value;
        Assert.Equal((_a, approvedAt, _b, approvedAt.AddSeconds(30), true), (profile.Approved, profile.ApprovedAt, profile.Live, profile.CheckedAt, profile.Changed));
        Assert.Equal(["services.heartbeat.every"], profile.ChangedPaths);
        Assert.Equal(_v.Progress.Current.AsOf, profile.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_any_event_it_answers_nulls_and_not_changed()
    {
        var profile = (await Service().Handle(new GetProfileRequest(), Token)).Value;

        Assert.Equal((null, null, null, null, false), (profile.Approved, profile.ApprovedAt, profile.Live, profile.CheckedAt, profile.Changed));
        Assert.Empty(profile.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Packs_lists_the_default_lens_pack_of_each_loop_as_built_in()
    {
        var packs = (await Service().Handle(new GetProfileRequest(), Token)).Value.Packs;

        Assert.Equal(
            [new ProfilePack("lenses.design", LensPackId.From("design-five"), "built-in", "sha256:" + new string('d', 64), "built-in", "ok"),
             new ProfilePack("lenses.review", LensPackId.From("code-review"), "built-in", "sha256:" + new string('r', 64), "built-in", "ok")],
            packs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Packs_adds_the_pack_a_repo_entry_chooses_and_reports_one_that_does_not_resolve()
    {
        var lenses = new LensPackSettings(Review: new PackChoice(Repos: new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "strict" }));
        var profile = new LoadedProfile(new SindriProfile(1, "t") { Workflow = new WorkflowSettings(Packs: new PackSettings(Lenses: lenses)) }, _a, "/p.yaml");
        var fake = Packs().ChoosingFor(LensPackKind.Review, "web", "strict");

        var packs = (await Service(fake, profile).Handle(new GetProfileRequest(), Token)).Value.Packs;

        var strict = Assert.Single(packs, static p => string.Equals(p.Id?.Value, "strict", StringComparison.Ordinal));
        Assert.Equal(("lenses.review", null, "none"), (strict.Kind, strict.Hash, strict.Pin));
        Assert.StartsWith("invalid: ", strict.Status, StringComparison.Ordinal);
        Assert.Equal(3, packs.Count);
    }

    private GetProfileService Service(FakeLensPacks? packs = null, LoadedProfile? profile = null) =>
        new(_v.Views, _v.Progress, packs ?? Packs(), new Approved(profile));

    private static FakeLensPacks Packs() => new FakeLensPacks().With(Pack("design-five", LensPackKind.Design, 'd')).With(Pack("code-review", LensPackKind.Review, 'r'));

    private static LensPack Pack(string id, LensPackKind applies, char hash) =>
        new(LensPackId.From(id), "sha256:" + new string(hash, 64), applies, [new PackLens(LensId.From("one"), "One", "prompt", LensWhen.Always)]);

    private sealed class Approved(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }
}
