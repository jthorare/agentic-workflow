using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class DirectorySessionDirectoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_directory_has_no_sessions()
    {
        var sessions = await new DirectorySessionDirectory(Path.Combine(_dir.Path, "absent"), new InMemoryProcessLiveness()).Live(CancellationToken.None);

        Assert.True(sessions.IsSuccess);
        Assert.Empty(sessions.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Records_that_cannot_be_read_are_skipped_and_extra_fields_ignored()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "1.json"), """{"pid":1,"sessionId":"good","entrypoint":"cli","extra":{"a":[1,2]}}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "2.json"), "{broken", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "3.json"), """{"pid":"three","sessionId":"x"}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "4.json"), """{"pid":1}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "notes.txt"), "ignored", TestContext.Current.CancellationToken);

        var liveness = new InMemoryProcessLiveness();
        liveness.Run(1, DateTimeOffset.UnixEpoch);
        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        var only = Assert.Single(sessions.Value);
        Assert.Equal(("good", string.Empty, 1), (only.Session.Value, only.Name, only.Pid));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_whose_pid_was_reused_by_a_process_that_started_after_the_record_was_written_is_not_live()
    {
        var file = Path.Combine(_dir.Path, "42.json");
        await File.WriteAllTextAsync(file, """{"pid":42,"sessionId":"old"}""", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(42, new DateTimeOffset(2026, 10, 17, 11, 0, 0, TimeSpan.Zero));

        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.Empty(sessions.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_whose_process_started_before_its_record_was_last_written_is_live()
    {
        var file = Path.Combine(_dir.Path, "42.json");
        await File.WriteAllTextAsync(file, """{"pid":42,"sessionId":"current"}""", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 17, 11, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(42, new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.Equal("current", Assert.Single(sessions.Value).Session.Value);
    }

    private Task WriteSession(int pid)
    {
        var text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"pid":{{pid}},"sessionId":"s{{pid}}"}""");
        return File.WriteAllTextAsync(Path.Combine(_dir.Path, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{pid}.json")), text, TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_sessions_are_checked_with_one_batch_not_one_call_each()
    {
        foreach (var pid in new[] { 41, 42, 43 })
        {
            await WriteSession(pid);
        }

        var liveness = new InMemoryProcessLiveness();
        _ = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.Equal((1, 0), (liveness.BatchCalls, liveness.SingleCalls));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pid_the_host_cannot_be_asked_about_is_kept_live_and_the_others_are_still_judged()
    {
        foreach (var pid in new[] { 41, 42, 43 })
        {
            await WriteSession(pid);
        }

        var liveness = new InMemoryProcessLiveness();
        liveness.Run(41, DateTimeOffset.UnixEpoch);
        liveness.FailWith(42, MetaErrors.HostUnreadable.With("unavailable on macOS: ps could not be run"));

        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.True(sessions.IsSuccess);
        Assert.Equal([41, 42], sessions.Value.Select(s => s.Pid));
    }

    public void Dispose() => _dir.Dispose();
}
