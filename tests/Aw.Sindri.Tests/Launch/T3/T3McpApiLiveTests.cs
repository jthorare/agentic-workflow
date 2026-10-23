using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>
/// The real MCP client against the T3 Code running on this box, read-only: it mints and revokes a token, reads the environment, looks a project up and
/// reads a thread that doesn't exist. It never starts a thread. Skipped unless AW_T3_LIVE=1 (T3 Code open, its 't3' CLI installed).
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class T3McpApiLiveTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static T3Options Options()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("AW_T3_LIVE"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("set AW_T3_LIVE=1 with T3 Code open to run the read-only checks against it");
        }

        var home = Environment.GetEnvironmentVariable("T3CODE_HOME") is { Length: > 0 } set ? set : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".t3");
        var cli = Environment.GetEnvironmentVariable("T3CODE_CLI_PATH") is { Length: > 0 } path ? path : Path.Combine(home, "bin", "t3");
        return new T3Options(Path.GetTempPath(), home, cli);
    }

    [Fact]
    public async Task The_real_T3_answers_the_reads_the_host_makes()
    {
        var options = Options();
        await using var credentials = new T3Credentials(options, TimeProvider.System);
        await using var api = new T3McpApi(credentials, options);

        var environment = await api.Environment(Token);
        var project = await api.ProjectFor("/no/such/repo/anywhere", Token);
        var thread = await api.Read("sindri-live-test-no-such-thread", Token);

        Expect.True(environment.IsSuccess && environment.Value.Id.Length > 0, environment.IsError ? environment.Error.Message : "environment");
        Expect.True(project is { IsSuccess: true, Value: null }, project.IsError ? project.Error.Message : "project");
        Expect.True(thread is { IsSuccess: true, Value: null }, thread.IsError ? thread.Error.Message : "thread");
    }
}
