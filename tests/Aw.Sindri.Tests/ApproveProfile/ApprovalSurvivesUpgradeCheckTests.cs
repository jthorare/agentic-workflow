using System.Text;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Profile;

namespace Aw.Sindri.Tests.ApproveProfile;

/// <summary>
/// The bug check for issue 1143: an upgrade that adds a field to the profile model made every stored approval unreadable (SND-PROFILE-005), and 'sindri profile approve' could not
/// show the live hash to fix it. The snapshot below is written the way an older build wrote it: its bytes hash to its directory name, but it lacks the 'index.ignore' and
/// 'index.demote' members today's model writes, so today's re-read produces other bytes and another hash.
/// </summary>
public sealed class ApprovalSurvivesUpgradeCheckTests : IDisposable
{
    private readonly ProfileFiles _files = new();
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _files.Dispose();

    private static LoadedProfile Live()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Index: new IndexSettings(DenyPaths: ["private/**"]))).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(ProfileReading.Canonical(profile, ProfileFiles.Options)), "/home/profile.yaml");
    }

    /// <summary>Saves <paramref name="bytes"/> as the latest approval, under the hash of those bytes, the way an approval of an earlier build is on disk.</summary>
    private ProfileHash Store(byte[] bytes)
    {
        var hash = ProfileHashing.Of(bytes);
        var directory = Path.Combine(_files.Settings.ApprovalsDirectory, hash.Value);
        _ = Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "profile.json"), bytes);
        File.WriteAllText(Path.Combine(_files.Settings.ApprovalsDirectory, "latest"), hash.Value + "\n");
        return hash;
    }

    /// <summary>The live profile as an earlier build would have stored it: without the index members added since.</summary>
    private ProfileHash StoreAsAnEarlierBuild(LoadedProfile live)
    {
        var current = ProfileReading.Canonical(live.Profile, ProfileFiles.Options);
        var node = JsonNode.Parse(current)!.AsObject();
        var index = node["index"]!.AsObject();
        _ = index.Remove("ignore");
        _ = index.Remove("demote");
        var earlier = Encoding.UTF8.GetBytes(node.ToJsonString());
        Assert.NotEqual(current, earlier);   // the premise: today's canonical form has members the earlier one lacked
        return Store(earlier);
    }

    private ApproveProfileService Service() =>
        new(_h.Profiles, _files.Approvals, _h.Held, _h.Store, _h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog()));

    [Fact, Trait("Lane", "integration")]
    public async Task An_approval_stored_before_a_field_was_added_is_still_read_as_that_approval()
    {
        var hash = StoreAsAnEarlierBuild(Live());

        var latest = await _files.Approvals.Latest(Token);

        Assert.True(latest.IsSuccess, latest.IsError ? latest.Error.ToString() : null);
        Assert.Equal(hash, latest.Value!.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_preview_after_such_an_upgrade_shows_the_live_hash()
    {
        var live = Live();
        _h.Profiles.Live = live;
        _ = StoreAsAnEarlierBuild(live);

        var shown = await Service().Handle(new ApproveProfileRequest(Hash: null, Answer: null), Token);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        Assert.Equal(live.Hash, shown.Value.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_preview_still_shows_the_live_hash_when_the_stored_approval_really_cannot_be_read()
    {
        var live = Live();
        _h.Profiles.Live = live;
        _ = Store(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"user\":\"thor\",\"aKeyNoBuildKnows\":true}"));

        var shown = await Service().Handle(new ApproveProfileRequest(Hash: null, Answer: null), Token);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        Assert.Equal((live.Hash, false), (shown.Value.Hash, shown.Value.Approved));
        Assert.NotEmpty(shown.Value.Changes);   // the diff against "nothing readable"
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wrong_hash_is_refused_with_the_exact_command_to_run()
    {
        var live = Live();
        _h.Profiles.Live = live;

        var refused = await Service().Handle(new ApproveProfileRequest(ProfileHash.From(new string('0', 64)), Answer: null), Token);

        Assert.Equal(SindriErrors.ProfileHashMismatch.Code, refused.Error.Code);
        Assert.Contains($"sindri profile approve {live.Hash.Value}", refused.Error.Message, StringComparison.Ordinal);
    }
}
