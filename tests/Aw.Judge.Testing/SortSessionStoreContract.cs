using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Xunit;

namespace Aw.Judge.Testing;

/// <summary>
/// The ISortSessionStore contract: the same tests run against the in-memory fake and the file adapter. A derived class
/// supplies a fresh store and one that can't write.
/// </summary>
public abstract class SortSessionStoreContract
{
    private static readonly SessionId _session = SessionId.From("sess-1");

    private static readonly SortSession _state = new(3, new Dictionary<string, int>(StringComparer.Ordinal) { ["brief"] = 2 }, new SortRequirements(UiEvidence: true));

    /// <summary>A fresh store with no session.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ISortSessionStore> Create(CancellationToken cancellationToken);

    /// <summary>A store that can't write.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ISortSessionStore> CreateUnavailable(CancellationToken cancellationToken);

    /// <summary>A session nobody wrote reads as the empty state.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_session_nobody_wrote_reads_as_empty()
    {
        var store = await Create(CancellationToken.None);

        var read = await store.Read(_session, CancellationToken.None);

        Expect.That(read.IsSuccess && read.Value.Prompts == 0 && read.Value.LastFired.Count == 0 && !read.Value.Requirements.UiEvidence, "no session is the empty state");
    }

    /// <summary>A written state reads back whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_written_state_reads_back()
    {
        var store = await Create(CancellationToken.None);

        Expect.That((await store.Write(_session, _state, CancellationToken.None)).IsSuccess, "the state is written");

        var read = await store.Read(_session, CancellationToken.None);
        Expect.That(read.IsSuccess && read.Value.Prompts == 3 && read.Value.LastFired["brief"] == 2 && read.Value.Requirements.UiEvidence, "every field survives");
    }

    /// <summary>A session id that can't be a file name is refused on write and reads as empty.</summary>
    /// <param name="session">The session id.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("")]
    public async Task A_session_id_that_cant_be_a_file_name_is_refused(string session)
    {
        var store = await Create(CancellationToken.None);
        var id = SessionId.From(session);

        Expect.That((await store.Write(id, _state, CancellationToken.None)).IsError, "the write is refused");
        var read = await store.Read(id, CancellationToken.None);
        Expect.That(read.IsSuccess && read.Value.Prompts == 0, "the read is the empty state");
    }

    /// <summary>A store that can't write answers with an error and throws nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unwritable_store_answers_with_an_error()
    {
        var store = await CreateUnavailable(CancellationToken.None);

        Expect.That((await store.Write(_session, _state, CancellationToken.None)).IsError, "Write is an error");
    }
}
