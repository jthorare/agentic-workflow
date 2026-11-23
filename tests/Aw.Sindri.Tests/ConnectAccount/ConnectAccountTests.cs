using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>ConnectAccount for GitHub (first-run spec decisions 13 and 14, §7 row A8): a gh login connects GitHub through `command:gh auth token`, and no token is copied or stored.</summary>
public sealed class ConnectAccountTests
{
    private const string GhCommand = "command:gh auth token";

    private readonly ConnectAccountRig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Gh_login_sets_review_credential_to_the_gh_command()
    {
        _rig.Gh.SignedInAs = "ada";
        using var entered = _rig.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await _rig.Service().Handle(new ConnectAccountRequest("github"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(new ConnectAccountResponse("github", "connected"), result.Value);
        Assert.Contains($"text review.credential={GhCommand}", _rig.Writer.Calls, StringComparer.Ordinal);
    }
}
