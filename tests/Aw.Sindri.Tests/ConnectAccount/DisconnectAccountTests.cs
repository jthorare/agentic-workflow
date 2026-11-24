using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Connections;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>DELETE /sindri/setup/accounts/github: undoes the accounts step's GitHub writes and nothing else; the owner's only.</summary>
public sealed class DisconnectAccountTests
{
    private readonly ConnectAccountRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private DisconnectAccountService Service() => new(_rig.Slice.Callers, _rig.Flow, _rig.Connections, _rig.Secrets, _rig.Events(), _rig.ProfileWrites());

    private void ReviewCredential(string credential)
    {
        var profile = new SindriProfile(1, "o") { Review = new PrReviewSettings(Credential: credential) };
        _rig.Profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Disconnecting_github_removes_both_setup_rows_the_device_token_and_the_gh_review_credential()
    {
        _ = (await _rig.Connections.Insert(GhRows.Row(), Token)).Value;
        var device = ConnectionSamples.Oauth(DeviceAccountStore.Name);
        _ = (await _rig.Connections.Insert(device, Token)).Value;
        _ = _rig.Secrets.Store(device.Name, new TokenSet("gho_device", RefreshToken: null, ExpiresAt: null)).Value;
        _ = (await _rig.Connections.Insert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), Token)).Value;
        ReviewCredential(GitHubCliCredential.Command);
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new DisconnectAccountRequest("github"), Token);

        Assert.Equal(new ConnectAccountResponse("github", "disconnected"), result.Value);
        Assert.Equal(["github:work"], _rig.Connections.Rows.Select(static c => c.Name.ToString()), StringComparer.Ordinal);   // a hand-added connection stays
        _ = Assert.Single(_rig.Secrets.Deleted);
        Assert.Equal(["remove review.credential", "save"], _rig.Writer.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_review_credential_the_owner_set_by_hand_is_kept()
    {
        ReviewCredential("env:MY_TOKEN");
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new DisconnectAccountRequest("github"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Empty(_rig.Writer.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launched_session_cannot_disconnect_an_account()
    {
        _ = (await _rig.Connections.Insert(GhRows.Row(), Token)).Value;
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Launched().Build());

        var result = await Service().Handle(new DisconnectAccountRequest("github"), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        _ = Assert.Single(_rig.Connections.Rows);
    }
}
