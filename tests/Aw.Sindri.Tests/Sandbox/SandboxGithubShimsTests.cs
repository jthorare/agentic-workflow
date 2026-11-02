using System.Diagnostics;
using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The agent-app shims (SB-7): the gh shim, the git credential helper and the branch-only pre-push hook run for real against a fake gh and a token file.</summary>
[Lane(TestLane.Integration)]
[UnsupportedOSPlatform("windows")]
public sealed class SandboxGithubShimsTests : IDisposable
{
    private const string Token = "ghs_secretToken123";
    private const string Namespace = "sindri/run-7";
    private const string Zeros = "0000000000000000000000000000000000000000";
    private const string Sha = "1234567890123456789012345678901234567890";

    private readonly TempRunArea _area = new();
    private readonly string _home;

    public SandboxGithubShimsTests()
    {
        _home = _area.Root + "/session";
        Directory.CreateDirectory(SessionHomePaths.GhTokenDirectory(_home));
        File.WriteAllText(SessionHomePaths.GhTokenFile(_home), Token + "\n");
        Directory.CreateDirectory(_area.Root + "/tools");
        _ = ExecutableScript.Write(_area.Root + "/tools/gh", "echo \"token=$GH_TOKEN\"\nfor a in \"$@\"; do echo \"arg=$a\"; done\n");
        Assert.Null(RunAreaPreparer.Prepare(AgentApp()));
    }

    public void Dispose() => _area.Dispose();

    [Fact]
    public void The_gh_shim_passes_its_arguments_and_the_token_file_content_in_gh_own_environment_only()
    {
        var (output, _, code) = Run("gh", ["pr", "list"], stdin: string.Empty);

        Assert.Equal(0, code);
        Assert.Equal($"token={Token}\narg=pr\narg=list\n", output);
        Assert.DoesNotContain(Token, File.ReadAllText(_area.Root + "/area/shims/gh"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_gh_shim_reads_the_token_at_each_call()
    {
        File.WriteAllText(SessionHomePaths.GhTokenFile(_home), "ghs_refreshed\n");

        var (output, _, _) = Run("gh", [], stdin: string.Empty);

        Assert.Equal("token=ghs_refreshed\n", output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_gh_shim_fails_with_a_clear_line_when_the_token_file_is_missing_or_empty(bool empty)
    {
        if (empty)
        {
            File.WriteAllText(SessionHomePaths.GhTokenFile(_home), string.Empty);
        }
        else
        {
            File.Delete(SessionHomePaths.GhTokenFile(_home));
        }

        var (output, error, code) = Run("gh", ["pr", "list"], stdin: string.Empty);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains("the GitHub token is not available", error, StringComparison.Ordinal);
        Assert.Contains(SandboxStatusLine.Refusal, error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_credential_helper_answers_get_for_https_github_com_with_the_token()
    {
        var (output, _, code) = Run("git-credential-sindri", ["get"], "protocol=https\nhost=github.com\npath=a/b\n");

        Assert.Equal(0, code);
        Assert.Equal($"username=x-access-token\npassword={Token}\n", output);
    }

    [Theory]
    [InlineData("protocol=https\nhost=example.com\n")]
    [InlineData("protocol=http\nhost=github.com\n")]
    [InlineData("host=github.com\n")]
    public void The_credential_helper_prints_nothing_for_another_host_or_protocol(string request)
    {
        var (output, _, code) = Run("git-credential-sindri", ["get"], request);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, output);
    }

    [Theory]
    [InlineData("store")]
    [InlineData("erase")]
    public void The_credential_helper_ignores_store_and_erase(string verb)
    {
        var (output, _, code) = Run("git-credential-sindri", [verb], "protocol=https\nhost=github.com\nusername=x\npassword=y\n");

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, output);
    }

    [Theory]
    [InlineData("refs/heads/sindri/run-7", Sha, true)]
    [InlineData("refs/heads/sindri/run-7/sub", Sha, true)]
    [InlineData("refs/heads/main", Sha, false)]
    [InlineData("refs/heads/sindri/run-70", Sha, false)]
    [InlineData("refs/tags/sindri/run-7", Sha, false)]
    [InlineData("refs/heads/sindri/run-7", Zeros, false)]
    public void The_pre_push_hook_allows_only_the_run_branch_and_never_a_delete(string remoteRef, string localSha, bool allowed)
    {
        var (_, error, code) = RunHook(AgentApp(), $"refs/heads/work {localSha} {remoteRef} {Zeros}\n");

        Assert.Equal(allowed ? 0 : 1, code);
        if (!allowed)
        {
            Assert.Equal($"git push to {remoteRef} is not allowed in this Step: only the run branch refs/heads/{Namespace} can be pushed. {SandboxStatusLine.Refusal}\n", error);
        }
    }

    [Fact]
    public void The_pre_push_hook_refuses_a_push_when_any_one_ref_is_not_the_run_branch()
    {
        var (_, _, code) = RunHook(AgentApp(), $"refs/heads/a {Sha} refs/heads/{Namespace} {Zeros}\nrefs/heads/b {Sha} refs/heads/main {Zeros}\n");

        Assert.Equal(1, code);
    }

    [Fact]
    public void The_pre_push_hook_refuses_everything_with_an_empty_namespace()
    {
        var (_, _, code) = RunHook(AgentApp() with { RunNamespace = string.Empty }, $"refs/heads/a {Sha} refs/heads/main {Zeros}\n");

        Assert.Equal(1, code);
    }

    [Fact]
    public void A_none_plan_keeps_the_refusing_hook_and_writes_no_gh_or_helper_shim()
    {
        using var other = new TempRunArea();
        var plan = other.Plan with { Github = GithubCredentials.None, Environment = [.. other.Plan.Environment, new PlanEnvVar("AW_SINDRI_HOME", other.Root + "/session", "test")] };

        Assert.Null(RunAreaPreparer.Prepare(plan));

        Assert.Contains("git push is not available in a sandboxed session", File.ReadAllText(other.Root + "/area/githooks/pre-push"), StringComparison.Ordinal);
        Assert.Contains(SandboxStatusLine.Refusal, File.ReadAllText(other.Root + "/area/githooks/pre-push"), StringComparison.Ordinal);
        Assert.False(File.Exists(other.Root + "/area/shims/gh"));
        Assert.False(File.Exists(other.Root + "/area/shims/" + SandboxShims.CredentialHelperName));
    }

    [Fact]
    public void Preparing_the_same_area_for_agent_app_after_none_replaces_the_hook_and_back()
    {
        using var other = new TempRunArea();
        var none = other.Plan with { Github = GithubCredentials.None };
        var app = AgentAppIn(other);

        Assert.Null(RunAreaPreparer.Prepare(none));
        Assert.Null(RunAreaPreparer.Prepare(app));
        Assert.Contains("refs/heads/", File.ReadAllText(other.Root + "/area/githooks/pre-push"), StringComparison.Ordinal);
        Assert.Null(RunAreaPreparer.Prepare(none));

        Assert.Contains("git push is not available", File.ReadAllText(other.Root + "/area/githooks/pre-push"), StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_real_gh_on_the_path_only_the_helper_is_written()
    {
        using var other = new TempRunArea();
        var plan = AgentAppIn(other, withGh: false);

        Assert.Null(RunAreaPreparer.Prepare(plan));

        Assert.False(File.Exists(other.Root + "/area/shims/gh"));
        Assert.True(File.Exists(other.Root + "/area/shims/" + SandboxShims.CredentialHelperName));
    }

    private SandboxPlan AgentApp() => AgentAppIn(_area);

    // The plan's PATH carries the host toolchain directory after the run area's shims; the session home is the plan's AW_SINDRI_HOME.
    private static SandboxPlan AgentAppIn(TempRunArea area, bool withGh = true)
    {
        Directory.CreateDirectory(area.Root + "/tools");
        if (withGh && !File.Exists(area.Root + "/tools/gh"))
        {
            _ = ExecutableScript.Write(area.Root + "/tools/gh", string.Empty);
        }

        return area.Plan with
        {
            Github = GithubCredentials.AgentApp,
            RunNamespace = Namespace,
            Environment =
            [
                .. area.Plan.Environment,
                new PlanEnvVar("AW_SINDRI_HOME", area.Root + "/session", "test"),
                new PlanEnvVar("PATH", $"{area.Root}/area/shims:{area.Root}/tools", "test"),
            ],
        };
    }

    private (string Output, string Error, int Code) RunHook(SandboxPlan plan, string stdin)
    {
        Assert.Null(RunAreaPreparer.Prepare(plan));
        return Start(_area.Root + "/area/githooks/pre-push", ["origin", "https://github.com/a/b"], stdin);
    }

    private (string Output, string Error, int Code) Run(string shim, string[] args, string stdin) =>
        Start(_area.Root + "/area/shims/" + shim, args, stdin);

    private static (string Output, string Error, int Code) Start(string script, string[] args, string stdin)
    {
        var start = new ProcessStartInfo(script) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        _ = start.Environment.Remove("GH_TOKEN");
        using var process = ExecutableScript.StartWhenNotBusy(start);   // the shim was written by the run-area preparer a moment ago
        try
        {
            process.StandardInput.Write(stdin);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // a shim that exits without reading its input (the credential helper on store and erase) closes the pipe first: that is its answer, not a failure of the runner
        }
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (output, error, process.ExitCode);
    }
}
