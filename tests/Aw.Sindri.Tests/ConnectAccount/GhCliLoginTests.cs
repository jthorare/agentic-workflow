using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>The gh login read (first-run spec decision 13) and the `command:` credential run (decision 14), over a fake tool runner: no gh is started.</summary>
public sealed class GhCliLoginTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData("github.com\n  ✓ Logged in to github.com account ada (keyring)\n", "")]
    [InlineData("", "github.com\n  ✓ Logged in to github.com as ada (oauth_token)\n")]
    public async Task A_passing_gh_auth_status_names_the_login(string output, string error)
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, output, error));

        var login = await new GhCliLogin(runner).Login(Token);

        Assert.Equal("ada", login);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("gh", call.FileName);
        Assert.Equal(["auth", "status", "--hostname", "github.com"], call.Arguments);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_signed_out_or_missing_gh_is_no_login()
    {
        var signedOut = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, "", "You are not logged into any GitHub hosts. To log in, run: gh auth login\n"));

        Assert.Null(await new GhCliLogin(signedOut).Login(Token));
        Assert.Null(await new GhCliLogin(new FakeToolRunner(ToolRun.Missing)).Login(Token));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_credential_runs_its_argv_with_no_shell_and_returns_the_trimmed_token()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "gho-from-gh\n"));

        var token = new CommandCredentials(runner).Token("command:gh auth token");

        Assert.Equal("gho-from-gh", token);
        var call = Assert.Single(runner.Calls);
        Assert.Equal("gh", call.FileName);
        Assert.Equal(["auth", "token"], call.Arguments);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failing_command_or_a_non_command_credential_gives_no_token()
    {
        Assert.Null(new CommandCredentials(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, "partial"))).Token("command:gh auth token"));
        Assert.Null(new CommandCredentials(new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, "x"))).Token("env:GH_TOKEN"));
    }
}
