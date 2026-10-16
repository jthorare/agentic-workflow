using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.claim.ref' (engine spec §5.7, §12; §10 'Claims across attempts'; engine P6 unit (d)) against a real temp repo
/// with one commit: the claim is the 'workflow_claims' row, the ref 'sindri/{user}/{item}' a marker at the default
/// branch's head; another live attempt gets 'claimExists', an ended owner's claim is taken over and reads 'created'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ClaimRefEffectTests
{
    private static readonly OutcomeName _created = OutcomeName.From("created");
    private static readonly OutcomeName _claimExists = OutcomeName.From("claimExists");

    private readonly InMemoryWorkflowClaims _claims = new();
    private readonly FakeInstanceLiveness _liveness = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Claim_ref_creates_the_marker_ref_at_the_default_branch_head()
    {
        using var repo = await TempGitRepo.Create();
        await Approve(repo);
        var head = await repo.Git("rev-parse", "main");

        var done = await Dispatch("T-3/item/1", attempt: 1);

        Assert.Equal(new EffectDone(_created, Existed: false), done);
        Assert.Equal($"{head} refs/sindri/dev/T-3", await repo.Git("show-ref", "sindri/dev/T-3"));
        Assert.Equal("main", await repo.Git("branch", "--format=%(refname:short)"));   // branches left alone
        Assert.Equal(new WorkflowClaim(ItemKey.From("T-3"), InstanceAddress.From("T-3/item/1"), 1), (await _claims.Find(ItemKey.From("T-3"), Token)).Value);
    }

    [Fact]
    public async Task Dispatching_the_same_claim_again_is_harmless()
    {
        using var repo = await TempGitRepo.Create();
        await Approve(repo);
        _ = await Dispatch("T-3/item/1", attempt: 1);

        var again = await Dispatch("T-3/item/1", attempt: 1);

        Assert.Equal(new EffectDone(_created, Existed: true), again);
    }

    [Fact]
    public async Task A_second_live_attempt_gets_claimExists_and_writes_nothing()
    {
        using var repo = await TempGitRepo.Create();
        await Approve(repo);
        _ = _liveness.Live.Add(InstanceAddress.From("T-3/item/1"));
        _ = await Dispatch("T-3/item/1", attempt: 1);

        var second = await Dispatch("T-3/item/2", attempt: 2);

        Assert.Equal(new EffectDone(_claimExists, Existed: false), second);
        Assert.Equal(InstanceAddress.From("T-3/item/1"), (await _claims.Find(ItemKey.From("T-3"), Token)).Value!.Owner);
    }

    [Fact]
    public async Task Claim_owned_by_ended_attempt_reads_created()
    {
        using var repo = await TempGitRepo.Create();
        await Approve(repo);
        _ = await Dispatch("T-3/item/1", attempt: 1);   // attempt 1 claimed, then ended: it isn't live
        var marker = await repo.Git("show-ref", "sindri/dev/T-3");

        var next = await Dispatch("T-3/item/2", attempt: 2);

        Assert.Equal(_created, next.Reply);
        Assert.Equal(new WorkflowClaim(ItemKey.From("T-3"), InstanceAddress.From("T-3/item/2"), 2), (await _claims.Find(ItemKey.From("T-3"), Token)).Value);
        Assert.Equal(marker, await repo.Git("show-ref", "sindri/dev/T-3"));   // the ref is reused
    }

    [Fact]
    public async Task A_repo_without_a_commit_is_an_error_the_activity_retries()
    {
        using var repo = await TempGitRepo.Create(commits: 0);
        await Approve(repo);

        var result = await Effect().Dispatch(Context("T-3/item/1", 1), Token);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task An_item_naming_a_repo_the_approved_profile_lacks_is_an_error()
    {
        using var repo = await TempGitRepo.Create();
        await Approve(repo, name: "other-repo");

        var result = await Effect().Dispatch(Context("T-3/item/1", 1), Token);

        Assert.True(result.IsError);
        Assert.Contains("dev-repo", result.Error.Message, StringComparison.Ordinal);
    }

    private ClaimRefEffect Effect() => new(_claims, _liveness, new GitClaimRefs(), _facts, _profiles);

    private async Task<EffectDone> Dispatch(string instance, int attempt)
    {
        var done = await Effect().Dispatch(Context(instance, attempt), Token);
        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        return done.Value;
    }

    private EffectContext Context(string instance, int attempt)
    {
        var stored = _facts.Put(Encoding.UTF8.GetBytes("""{ "id": "T-3", "repo": "dev-repo", "spec": "the spec" }"""), Token).AsTask().GetAwaiter().GetResult().Value;
        using var fact = JsonDocument.Parse($$"""{"ref":"{{stored.Ref}}","sha256":"{{stored.Sha256}}","headSha":null}""");
        var facts = new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:item")] = fact.RootElement.Clone() };
        return new EffectContext(BlockId.From("effect.claim.ref"), $"{instance}:2:0", "C-" + instance, FromEngine: false, instance, "T-3", attempt,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, Text: null);
    }

    private async Task Approve(TempGitRepo repo, string name = "dev-repo")
    {
        var profile = new SindriProfile(1, "dev", Repos: [new RepoEntry(name, repo.Path)], Workflow: new WorkflowSettings(Effects: ["effect.claim.ref"]));
        _ = await _profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), Token);
    }
}
