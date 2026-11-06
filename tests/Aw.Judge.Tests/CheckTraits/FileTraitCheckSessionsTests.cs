using Aw.Judge.Core.Ids;
using Aw.Judge.Infrastructure.Traits;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.CheckTraits;

// The repos a session edited files in: '<judge dir>/trait-check/sessions/<sid>', one repo root per line, read back by
// the Stop check. Best effort like the log: a record that can't be written or read is no repos, never an error.
public sealed class FileTraitCheckSessionsTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    private readonly TempStateDirectory _state = new();

    private string Records => Path.Combine(_state.Path, "trait-check", "sessions");

    [Fact, Trait("Lane", "unit")]
    public void A_session_keeps_each_repo_once_in_the_order_it_was_first_edited()
    {
        var sessions = new FileTraitCheckSessions(Records, new FakeTimeProvider(_now));

        sessions.Add(Id("s1"), "/r/b");
        sessions.Add(Id("s1"), "/r/a");
        sessions.Add(Id("s1"), "/r/b");
        sessions.Add(Id("s2"), "/r/c");

        Assert.Equal(["/r/b", "/r/a"], sessions.Repos(Id("s1")));
        Assert.Equal(["/r/b", "/r/a"], File.ReadAllLines(Path.Combine(Records, "s1.repos")));
        Assert.Empty(sessions.Repos(Id("s3")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_session_sweeps_records_untouched_for_a_week()
    {
        var sessions = new FileTraitCheckSessions(Records, new FakeTimeProvider(_now));
        sessions.Add(Id("old"), "/r/a");
        sessions.Add(Id("recent"), "/r/a");
        File.SetLastWriteTimeUtc(Path.Combine(Records, "old.repos"), _now.UtcDateTime.AddDays(-8));
        File.SetLastWriteTimeUtc(Path.Combine(Records, "recent.repos"), _now.UtcDateTime.AddDays(-6));

        sessions.Add(Id("new"), "/r/b");

        Assert.Empty(sessions.Repos(Id("old")));
        Assert.Equal(["/r/a"], sessions.Repos(Id("recent")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_that_cannot_be_written_is_dropped()
    {
        _ = Directory.CreateDirectory(Path.Combine(_state.Path, "trait-check"));
        File.WriteAllText(Records, "a file where the directory should be");
        var sessions = new FileTraitCheckSessions(Records, TimeProvider.System);

        sessions.Add(Id("s1"), "/r/a");

        Assert.Empty(sessions.Repos(Id("s1")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("")]
    public void A_session_id_that_cannot_be_a_file_name_records_nothing(string id)
    {
        var sessions = new FileTraitCheckSessions(Records, TimeProvider.System);

        sessions.Add(Id(id), "/r/a");

        Assert.Empty(sessions.Repos(Id(id)));
        Assert.False(Directory.Exists(Records));
    }

    public void Dispose() => _state.Dispose();

    private static SessionId Id(string value) => SessionId.From(value);
}
