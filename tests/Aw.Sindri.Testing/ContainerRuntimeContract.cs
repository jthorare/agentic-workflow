using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IContainerRuntime"/> honours: it reports the facts of the box it asks, and never throws
/// for a missing, stopped or hung runtime. Run against the fake and, over a scripted tool runner, the docker CLI adapter.
/// </summary>
public abstract class ContainerRuntimeContract
{
    /// <summary>A runtime on a box whose facts are <paramref name="facts"/>.</summary>
    /// <param name="facts">The facts the box has.</param>
    /// <returns>The runtime.</returns>
    protected abstract IContainerRuntime Create(ContainerRuntimeFacts facts);

    /// <summary>A healthy box reports every fact.</summary>
    [Fact]
    public void A_healthy_box_reports_every_fact()
        => Expect.Equal(FakeContainerRuntime.Healthy, Create(FakeContainerRuntime.Healthy).Inspect(TestContext.Current.CancellationToken));

    /// <summary>No docker CLI reports nothing else either.</summary>
    [Fact]
    public void A_box_without_the_cli_reports_it_missing()
    {
        var facts = FakeContainerRuntime.NoCli;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>The CLI with the daemon down still reports Compose.</summary>
    [Fact]
    public void A_stopped_daemon_is_reported_with_compose_still_known()
    {
        var facts = FakeContainerRuntime.DaemonDown;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>A socket this user may not open is told apart from a stopped daemon.</summary>
    [Fact]
    public void A_socket_permission_problem_is_reported_as_such()
    {
        var facts = FakeContainerRuntime.PermissionDenied;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>A daemon that stays silent past the retry is told apart from a stopped one.</summary>
    [Fact]
    public void A_daemon_that_does_not_answer_in_time_is_reported_as_starting()
    {
        var facts = FakeContainerRuntime.Starting;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>A missing docker context is told apart from a stopped daemon.</summary>
    [Fact]
    public void A_missing_docker_context_is_reported_as_such()
    {
        var facts = FakeContainerRuntime.ContextMissing;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }

    /// <summary>Only legacy docker-compose: Compose v2 isn't there.</summary>
    [Fact]
    public void A_box_with_only_compose_v1_reports_no_compose_v2()
    {
        var facts = FakeContainerRuntime.ComposeV1Only;

        Expect.Equal(facts, Create(facts).Inspect(TestContext.Current.CancellationToken));
    }
}
