using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Xunit;

namespace Aw.Sindri.Testing.Launch;

/// <summary>The contract every <see cref="ISessionHost"/> honours, run against the fake and (U5) the tmux adapter.</summary>
public abstract class SessionHostContract
{
    /// <summary>The host under test.</summary>
    /// <returns>The host.</returns>
    protected abstract ISessionHost Create();

    /// <summary>A session that stays alive until stopped, named <paramref name="name"/>.</summary>
    /// <param name="name">The session name.</param>
    /// <returns>The session.</returns>
    protected abstract HostSession Session(string name);

    /// <summary>Start returns a reference named for the session, of the host's kind.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Start_returns_a_ref_named_for_the_session()
    {
        var host = Create();

        var started = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);

        Expect.True(started.IsSuccess);
        Expect.Equal(new HostRef(host.Kind, "snd-one"), started.Value);
    }

    /// <summary>A name that is live is not started twice.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Starting_a_live_name_again_is_a_conflict()
    {
        var host = Create();
        _ = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);

        var again = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);

        Expect.True(again.IsError && again.Error.Kind == ErrorKind.Conflict);
    }

    /// <summary>Stop is safe to repeat, and for a session that never existed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Stop_is_idempotent()
    {
        var host = Create();
        var started = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);

        var first = await host.Stop(started.Value, CancellationToken.None);
        var second = await host.Stop(started.Value, CancellationToken.None);
        var never = await host.Stop(new HostRef(host.Kind, "snd-never"), CancellationToken.None);

        Expect.True(first.IsSuccess && second.IsSuccess && never.IsSuccess);
    }

    /// <summary>A stopped name can be started again.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_stopped_name_can_be_started_again()
    {
        var host = Create();
        var started = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);
        _ = await host.Stop(started.Value, CancellationToken.None);

        var again = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);

        Expect.True(again.IsSuccess);
    }

    /// <summary>No client is attached to a new session, or to one that is gone.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_client_is_attached_to_a_new_or_gone_session()
    {
        var host = Create();
        var started = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);
        Expect.Equal(0, await host.AttachedClients(started.Value, CancellationToken.None));

        _ = await host.Stop(started.Value, CancellationToken.None);

        Expect.Equal(0, await host.AttachedClients(started.Value, CancellationToken.None));
    }

    /// <summary>A started session exists until it is stopped; a name never started does not.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_session_exists_from_its_start_until_its_stop()
    {
        var host = Create();
        var started = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);
        Expect.True(await host.Exists(started.Value, CancellationToken.None));
        Expect.True(!await host.Exists(new HostRef(host.Kind, "snd-never"), CancellationToken.None));

        _ = await host.Stop(started.Value, CancellationToken.None);

        Expect.True(!await host.Exists(started.Value, CancellationToken.None));
    }

    /// <summary>A started session has a command that shows it (a tmux attach, a T3 thread link); each adapter's own tests check what it names.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_started_session_has_an_attach_command()
    {
        var host = Create();

        var started = await host.Start(Session("snd-one"), TimeSpan.FromSeconds(4), CancellationToken.None);

        Expect.True(host.AttachArgv(started.Value).Count > 0);
    }

    /// <summary>The probe reports the host's own kind.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_probe_reports_the_hosts_kind()
    {
        var host = Create();

        var facts = await host.Probe(CancellationToken.None);

        Expect.Equal(host.Kind, facts.Kind);
    }
}
