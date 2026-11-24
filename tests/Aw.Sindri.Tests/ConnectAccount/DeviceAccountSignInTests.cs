using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>The accounts step's background device poll (first-run spec decisions 13, 14, 17): GitHub's yes stores the token once, a cancel or a newer code stores nothing and never throws.</summary>
public sealed class DeviceAccountSignInTests
{
    private readonly ConnectAccountRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task StartAsOwner()
    {
        _rig.OwnerApp("Iv1.owner-app");
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());
        _ = (await _rig.Service().Handle(new ConnectAccountRequest("github"), Token)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Githubs_yes_stores_the_token_as_github_default_and_clears_skip_accounts_without_a_review_credential()
    {
        await StartAsOwner();

        _rig.Device.Finish.SetResult(new TokenSet("gho_device", RefreshToken: null, ExpiresAt: null));
        await _rig.Flow.Polling!.WaitAsync(Token);   // why: StartAsOwner started the poll

        var row = Assert.Single(_rig.Connections.Rows);
        Assert.Equal((DeviceAccountStore.Name, ConnectionKind.OAuth, "github", ConnectionStatus.Connected), (row.Name, row.Kind, row.Preset, row.Status));
        Assert.Equal("Iv1.owner-app", row.Config["clientId"]);
        Assert.Equal("gho_device", _rig.Secrets.Tokens(Assert.IsType<SecretPointer>(row.SecretPointer).ToString())?.AccessToken);
        Assert.Equal(["remove setup.skipAccounts", "save"], _rig.Writer.Calls);   // decision 14 / Q1: review.credential stays unset
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancel_ends_the_poll_cleanly_with_nothing_stored()
    {
        await StartAsOwner();

        Assert.True(_rig.Flow.Cancel());
        await _rig.Flow.Polling!.WaitAsync(Token);   // why: StartAsOwner started the poll; it ends without throwing

        Assert.Equal((0, 0, 0), (_rig.Connections.Rows.Count, _rig.Secrets.Pointers.Count, _rig.Writer.Calls.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_newer_code_ends_the_older_poll()
    {
        await StartAsOwner();
        var older = _rig.Flow.Polling!;   // why: StartAsOwner started the poll

        await StartAsOwner();
        await older.WaitAsync(Token);

        Assert.True(older.IsCompletedSuccessfully);
        Assert.NotSame(older, _rig.Flow.Polling);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_connect_that_finds_a_gh_login_ends_the_poll_in_progress()
    {
        await StartAsOwner();
        var polling = _rig.Flow.Polling!;   // why: StartAsOwner started the poll
        _rig.Gh.SignedInAs = "ada";
        using (_rig.Slice.Callers.Enter(A.Caller().Human().Build()))
        {
            Assert.Equal("connected", (await _rig.Service().Handle(new ConnectAccountRequest("github"), Token)).Value.State);
        }

        await polling.WaitAsync(Token);
        _ = _rig.Device.Finish.TrySetResult(new TokenSet("gho_late", RefreshToken: null, ExpiresAt: null));   // a late yes from GitHub

        Assert.Equal(GitHubCliCredential.ConnectionName, Assert.Single(_rig.Connections.Rows).Name);
        Assert.Empty(_rig.Secrets.Pointers);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_connect_that_answers_token_needed_ends_the_poll_in_progress()
    {
        await StartAsOwner();
        var polling = _rig.Flow.Polling!;   // why: StartAsOwner started the poll
        _rig.Profiles.Live = SindriErrors.ProfileMissing.With("no profile");   // no client id any more
        using (_rig.Slice.Callers.Enter(A.Caller().Human().Build()))
        {
            Assert.Equal("token-needed", (await _rig.Service().Handle(new ConnectAccountRequest("github"), Token)).Value.State);
        }

        await polling.WaitAsync(Token);

        Assert.True(polling.IsCompletedSuccessfully);
        Assert.Empty(_rig.Connections.Rows);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_sign_in_stores_nothing()
    {
        await StartAsOwner();

        _rig.Device.Finish.SetResult(SindriErrors.ConnectionConsentDenied.With("denied"));
        await _rig.Flow.Polling!.WaitAsync(Token);   // why: StartAsOwner started the poll

        Assert.Equal((0, 0, 0), (_rig.Connections.Rows.Count, _rig.Secrets.Pointers.Count, _rig.Writer.Calls.Count));
    }
}
