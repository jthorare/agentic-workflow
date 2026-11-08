using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri workflow complete' from a launched agent session, end to end (#2163): the verb table, the header the CLI sends, the real caller middleware
/// and the real report authority, with nothing stubbed on the route. Only the three services that talk to Temporal are replaced, by one instance
/// whose open run is an agent's on a known launch; the launch's secret is on disk the way the launcher leaves it.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CompleteAsAgentCliTests : IDisposable
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-9/step/1");
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public CompleteAsAgentCliTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_launched_sessions_secret_is_accepted_as_the_agent_by_the_real_host()
    {
        var run = new AgentRun();
        await using var app = await Started(run, TestContext.Current.CancellationToken);
        var secret = Launched(app);

        await Complete(secret);

        Assert.DoesNotContain("SND-AUTH", Output(), StringComparison.Ordinal);
        Assert.True(run.Gets > 0, "the host never read the instance, so the report never reached the authority");
        Assert.Equal(StepRuntime.Agent, Assert.IsType<StepCompleted>(Assert.Single(run.Sent)).Via);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_session_without_the_secret_gets_SND_AUTH_002()
    {
        var run = new AgentRun();
        await using var app = await Started(run, TestContext.Current.CancellationToken);
        _ = Launched(app);

        await Complete(secret: null);

        Assert.Contains("SND-AUTH-002", Output(), StringComparison.Ordinal);
        Assert.Empty(run.Sent);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_wrong_secret_gets_SND_AUTH_003()
    {
        var run = new AgentRun();
        await using var app = await Started(run, TestContext.Current.CancellationToken);
        _ = Launched(app);

        await Complete("not-the-launch-secret");

        Assert.Contains("SND-AUTH-003", Output(), StringComparison.Ordinal);
        Assert.Empty(run.Sent);
    }

    /// <summary>Leaves the launch's hash on disk where the host's launch store reads it, as the launcher does; returns the secret the session would hold.</summary>
    private static string Launched(WebApplication app)
    {
        var secret = LaunchSecrets.Mint();
        LaunchSecrets.Write(app.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, _launch, secret);
        return secret;
    }

    /// <summary>Runs the verb as the session would: its environment holds the launch the launcher set and the secret, if any.</summary>
    private async Task Complete(string? secret) =>
        _ = await CommandTable.Run(
            ["workflow", "complete", _address.Value, "--run", "1.1", "--outcome", "done", "--nonce", new string('c', 32)],
            name => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_LAUNCH" => _launch.Value, "AW_SINDRI_LAUNCH_SECRET" => secret, _ => null },
            TimeProvider.System);

    private string Output() => _out + _error.ToString();

    private Task<WebApplication> Started(AgentRun run, CancellationToken ct) =>
        CliHost.Started(_home, ct, services: s => s
            .AddSingleton<IInstanceDirectory>(run)
            .AddSingleton<IInstanceReader>(run)
            .AddSingleton<IInstanceRuntime>(run)
            .AddSingleton<IApprovedRecipes>(new StepRunRecipes()));

    /// <summary>One instance whose open run is an agent's on <see cref="_launch"/>; records what is read and what is sent to it.</summary>
    private sealed class AgentRun : IInstanceDirectory, IInstanceRuntime
    {
        private readonly List<Trigger> _sent = [];
        private int _gets;

        public int Gets => Volatile.Read(ref _gets);

        public IReadOnlyList<Trigger> Sent => _sent;

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) => ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(_address);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("not started here");

        public ValueTask<Result<InstanceView>> Get(InstanceAddress at, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _gets);
            return ValueTask.FromResult<Result<InstanceView>>(View());
        }

        public ValueTask<Result<CommandResult>> Send(InstanceAddress at, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
        {
            _sent.Add(command);
            return ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(), "-> checks"));
        }

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress at, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException("not requested here");

        public ValueTask<Result<Unit>> Signal(InstanceAddress at, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException("not signalled here");

        private static InstanceView View() =>
            new(_address, new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "produce", Detail: null,
                DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, RunToken.For(1, 1), Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(_address),
                Version = 3,
                AgentRun = new OpenRun(RunToken.For(1, 1), StepRuntime.Agent, _launch.Value, Task: null),
            };
    }
}
