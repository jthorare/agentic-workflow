using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ApproveAll;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri recipe approve-all' against the real host (setup-once spec §5.3, §7.2): the plan is printed in order, the typed
/// 'approve-all &lt;digest&gt;' starts the job, a wrong phrase and a session are refused, and an empty plan says so and exits 2.
/// </summary>
public sealed class RecipeApproveAllCliTests : IDisposable
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Human_sees_the_plan_in_order_types_the_phrase_and_gets_the_started_job()
    {
        var ct = TestContext.Current.CancellationToken;
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        await using var host = await ApproveAllScenarios.Start(shipped, ct);
        await ViewScenarios.Approve(host, ct);
        var digest = (await shipped.Plan(ct)).Value.Digest;

        var code = await (await Command(host, human: true, $"approve-all {digest}", ct)).Run(json: false, ct);

        var text = _out.ToString();
        Assert.True(code == 0, _error.ToString());
        Assert.Contains($"approving 2 recipes, children first (digest {digest}):\n", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("workflow.step version 2", StringComparison.Ordinal) < text.IndexOf("workflow.item version 3", StringComparison.Ordinal));
        Assert.Contains($"Type 'approve-all {digest}' to confirm", text, StringComparison.Ordinal);
        Assert.Contains("approve-all started as job JOB-", text, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_wrong_phrase_is_refused_and_approves_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        await using var host = await ApproveAllScenarios.Start(shipped, ct);
        await ViewScenarios.Approve(host, ct);

        var code = await (await Command(host, human: true, "approve-all 000000", ct)).Run(json: false, ct);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-RECIPE-009 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, shipped.Applied);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_session_is_refused_and_approves_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var shipped = new FakeShippedRecipes(ApproveAllFixtures.StepAndItem());
        await using var host = await ApproveAllScenarios.Start(shipped, ct);
        await ViewScenarios.Approve(host, ct);

        var code = await (await Command(host, human: false, "approve-all 000000", ct)).Run(json: false, ct);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, shipped.Applied);
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task An_empty_plan_says_so_and_exits_2_without_a_challenge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ApproveAllScenarios.Start(new FakeShippedRecipes([]), ct);
        await ViewScenarios.Approve(host, ct);

        var code = await (await Command(host, human: true, string.Empty, ct)).Run(json: false, ct);

        Assert.Equal(2, code);
        Assert.Contains("Every recipe is approved.", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Type '", _out.ToString(), StringComparison.Ordinal);
    }

    private async Task<ApproveAllCommand> Command(GoldenHost host, bool human, string typed, CancellationToken ct)
    {
        var store = host.App.Services.GetRequiredService<IHumanTokenStore>();
        host.App.Services.GetRequiredService<HeldHumanToken>().Hold((await store.Mint(ct)).Value);   // the CLI reads the stored token, so the host holds that one
        var session = new AgentSession(human ? null : "CLAUDECODE is set", Terminal: true, Prover: false);
        var endpoint = new ControlEndpoint(new Uri(host.App.Urls.First()), GoldenHost.Pid, 1);
        var client = new ControlPlaneClient(FakeControlPlaneProbe.For(endpoint, new Ping(GoldenHost.Pid, 1)), SliceHarness.Json,
            CallerHeaders.For(session, store, static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));
        host.Own(client);
        var desk = new RecipeDesk(client, new StringReader(typed + "\n"), static _ => Task.FromResult<Error?>(null));
        return new ApproveAllCommand(desk, new Output(_out, _error, SliceHarness.Json));
    }
}
