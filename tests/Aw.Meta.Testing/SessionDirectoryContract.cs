using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The ISessionDirectory contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class SessionDirectoryContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A pid that is alive on every machine: init.</summary>
    protected const int AlivePid = 1;

    /// <summary>A pid no machine has.</summary>
    protected const int DeadPid = 2_147_000_000;

    /// <summary>A directory that lists exactly the sessions seeded: those whose pid is alive and those whose is not.</summary>
    /// <param name="alive">Sessions whose process runs.</param>
    /// <param name="dead">Sessions whose process is gone.</param>
    /// <returns>The directory.</returns>
    protected abstract ISessionDirectory Create(IReadOnlyList<LiveSession> alive, IReadOnlyList<LiveSession> dead);

    private static LiveSession Session(string name, string id, int pid) => new(SessionId.From(id), name, pid, "idle", "/work/" + name);

    /// <summary>Only sessions whose process is alive are listed, with every field.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Only_live_sessions_are_listed()
    {
        var live = Session("t1-api", "s-1", AlivePid);
        var directory = Create([live], [Session("t1-ui", "s-2", DeadPid)]);

        var sessions = await directory.Live(_none);

        Expect.That(sessions.IsSuccess && sessions.Value.SequenceEqual([live]), "the live session only, with every field");
    }

    /// <summary>A session's messaging socket is its address, the "from" its peer messages carry (#4103).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_messaging_socket_is_the_sessions_address()
    {
        var live = Session("t1-api", "s-1", AlivePid) with { Address = "uds:/run/user/1000/cc-socks/1.sock" };
        var directory = Create([live], []);

        var sessions = await directory.Live(_none);

        Expect.That(sessions.IsSuccess && sessions.Value.SequenceEqual([live]), "the session with its address");
    }

    /// <summary>With no sessions the list is empty.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task No_sessions_is_an_empty_list()
    {
        var sessions = await Create([], []).Live(_none);

        Expect.That(sessions.IsSuccess && sessions.Value.Count == 0, "empty");
    }
}
