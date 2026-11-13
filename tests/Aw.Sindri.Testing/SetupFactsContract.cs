using Aw.Kernel;
using Aw.Sindri.Core.Setup;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The ISetupFacts contract: the same tests run against the in-memory fake and every real adapter (.agents/rules/csharp.md, Seams).
/// A source never throws (a failure is an error in the Result), and asking twice with nothing changed answers the same: the reads start nothing.
/// </summary>
public abstract class SetupFactsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Asking each source twice answers the same, so a read changes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Asking_twice_with_nothing_changed_answers_the_same()
    {
        var facts = Create();

        Same(await facts.Repos(Token), await facts.Repos(Token));
        Same(await facts.Profile(Token), await facts.Profile(Token));
        Same(await facts.Mcp(Token), await facts.Mcp(Token));
        Same(await facts.Map(Token), await facts.Map(Token));
        Same(await facts.Sources(Token), await facts.Sources(Token));
        Same(await facts.Serve(Token), await facts.Serve(Token));
        Same(await facts.Agent(Token), await facts.Agent(Token));
        Same(await facts.FirstWorkflow(Token), await facts.FirstWorkflow(Token));
    }

    /// <summary>A box with nothing set up has no repo and no registration, and its map is not built.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_box_has_no_repo_no_registration_and_no_map()
    {
        var facts = Create();

        var repos = await facts.Repos(Token);
        var mcp = await facts.Mcp(Token);
        var map = await facts.Map(Token);

        Expect.True(repos.IsSuccess && repos.Value.Count == 0);
        Expect.True(mcp.IsSuccess && mcp.Value.Registered == 0);
        Expect.True(map.IsSuccess && map.Value.Phase != MapPhase.Built);
    }

    /// <summary>A box with nothing set up has no agent, has not run hello-sindri and has not dismissed it (first-run spec §3.1).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_box_has_no_agent_and_no_first_workflow_run()
    {
        var facts = Create();

        var agent = await facts.Agent(Token);
        var first = await facts.FirstWorkflow(Token);

        Expect.True(agent.IsSuccess && agent.Value.Target is null);
        Expect.True(first.IsSuccess && first.Value is { Succeeded: false, Dismissed: false });
    }

    /// <summary>Creates the implementation under test, over an empty box.</summary>
    /// <returns>A fresh ISetupFacts.</returns>
    protected abstract ISetupFacts Create();

    // A Result is a class without value equality: two successes agree on their value, two errors on their code.
    private static void Same<T>(Result<T> first, Result<T> second)
    {
        Expect.Equal(first.IsSuccess, second.IsSuccess);
        if (first.IsSuccess)
        {
            Expect.Equal(first.Value, second.Value);
        }
        else
        {
            Expect.Equal(first.Error.Code, second.Error.Code);
        }
    }
}
