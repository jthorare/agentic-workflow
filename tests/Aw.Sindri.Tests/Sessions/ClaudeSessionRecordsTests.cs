using System.Globalization;
using System.Runtime.Versioning;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Claude's session records (collector spec §4.1 step 3): '*.json' only, bounded, defensive.</summary>
public sealed class ClaudeSessionRecordsTests
{
    private static IReadOnlyList<Core.Sessions.ClaudeRecord> Read(string directory) =>
        new ClaudeSessionRecords(directory).Read(TestContext.Current.CancellationToken).Value;

    [Fact, Trait("Lane", "integration")]
    public void A_separate_agent_homes_session_records_are_read_beside_the_owners()
    {
        var owner = Directory.CreateTempSubdirectory("aw-owner-").FullName;
        var agentHome = Directory.CreateTempSubdirectory("aw-home-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(owner, "1.json"), """{ "pid": 1, "sessionId": "s1", "procStart": "10", "kind": "interactive" }""");
            File.WriteAllText(Path.Combine(agentHome, "2.json"), """{ "pid": 2, "sessionId": "s2", "procStart": "20", "kind": "interactive" }""");

            var records = new ClaudeSessionRecords(owner, [agentHome]).Read(TestContext.Current.CancellationToken).Value;

            Assert.Equal([1, 2], records.Select(static r => r.Pid).Order());
        }
        finally
        {
            Directory.Delete(owner, recursive: true);
            Directory.Delete(agentHome, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void Records_read_the_fields_the_collector_uses_and_procStart_as_a_string()
    {
        var records = Read(SessionFixtures.ClaudeSessions);

        var warp = records.Single(static r => r.Pid == 211);
        Assert.Equal([211, 311, 1100, 9999], records.Select(static r => r.Pid).Order());
        Assert.Equal(("aaaaaaaa-0000-4000-8000-000000000211", "1100", "interactive", "cli", "api work", "busy"), (warp.Session!.Value.Value, warp.ProcStart, warp.Kind, warp.Entrypoint, warp.Name, warp.Status));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1760000100211), warp.StatusUpdatedAt);
    }

    [Fact, Trait("Lane", "integration")]
    public void More_than_the_cap_keeps_the_most_recently_written_files()
    {
        var directory = Directory.CreateTempSubdirectory("aw-records-cap").FullName;
        try
        {
            var epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 1; i <= ClaudeSessionRecords.MaxFiles + 1; i++)
            {
                var file = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"{i}.json"));
                File.WriteAllText(file, string.Create(CultureInfo.InvariantCulture, $"{{ \"pid\": {i}, \"sessionId\": \"s{i}\", \"procStart\": \"1\", \"kind\": \"interactive\" }}"));
                File.SetLastWriteTimeUtc(file, epoch.AddMinutes(-i));   // 1.json is the newest, 257.json the oldest
            }

            var pids = Read(directory).Select(static r => r.Pid).ToList();

            Assert.Equal(ClaudeSessionRecords.MaxFiles, pids.Count);
            Assert.Contains(1, pids);
            Assert.DoesNotContain(ClaudeSessionRecords.MaxFiles + 1, pids);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    [UnsupportedOSPlatform("windows")]
    public void Key_files_are_never_opened()
    {
        var directory = Directory.CreateTempSubdirectory("aw-records").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "5.json"), """{ "pid": 5, "sessionId": "s5", "procStart": "50", "kind": "interactive" }""");
            var key = Path.Combine(directory, "5.abc123.key");
            File.WriteAllText(key, """{ "pid": 6, "sessionId": "from-a-key-file", "procStart": "60", "kind": "interactive" }""");
            File.SetUnixFileMode(key, UnixFileMode.None);   // opening it would throw; the reader must not even try

            var records = Read(directory);

            Assert.Equal([5], records.Select(static r => r.Pid));
            File.SetUnixFileMode(key, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void Only_the_used_fields_are_parsed_and_a_broken_or_oversized_file_is_skipped()
    {
        var directory = Directory.CreateTempSubdirectory("aw-records").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "1.json"), """{ "pid": 1, "sessionId": "ok", "messagingSocketPath": "/tmp/secret.sock", "bridgeSessionId": "b", "procStart": 77, "futureField": { "a": [1] } }""");
            File.WriteAllText(Path.Combine(directory, "2.json"), "{ not json");
            File.WriteAllText(Path.Combine(directory, "3.json"), $$"""{ "pid": 3, "name": "{{new string('x', (int)ClaudeSessionRecords.MaxBytes)}}" }""");
            File.WriteAllText(Path.Combine(directory, "4.json"), """{ "sessionId": "no-pid" }""");

            var records = Read(directory);

            var only = Assert.Single(records);
            Assert.Equal((1, "77"), (only.Pid, only.ProcStart));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void At_most_256_files_are_read_and_a_missing_directory_is_empty()
    {
        var directory = Directory.CreateTempSubdirectory("aw-records").FullName;
        try
        {
            for (var pid = 1; pid <= ClaudeSessionRecords.MaxFiles + 10; pid++)
            {
                File.WriteAllText(Path.Combine(directory, pid.ToString("D4", CultureInfo.InvariantCulture) + ".json"), string.Create(CultureInfo.InvariantCulture, $$"""{ "pid": {{pid}} }"""));
            }

            Assert.Equal(ClaudeSessionRecords.MaxFiles, Read(directory).Count);
            Assert.Empty(Read(Path.Combine(directory, "missing")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    [UnsupportedOSPlatform("windows")]
    public void An_unreadable_directory_is_a_failed_source_SND_SESSION_001_not_an_empty_list()
    {
        Assert.SkipWhen(Environment.IsPrivilegedProcess, "root reads a mode-0 directory");
        var directory = Directory.CreateTempSubdirectory("aw-records-denied").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "5.json"), """{ "pid": 5, "sessionId": "s5", "procStart": "50", "kind": "interactive" }""");
            File.SetUnixFileMode(directory, UnixFileMode.None);

            var result = new ClaudeSessionRecords(directory).Read(TestContext.Current.CancellationToken);

            Assert.True(result.IsError);
            Assert.Equal("SND-SESSION-001", result.Error.Code);
        }
        finally
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(directory, recursive: true);
        }
    }
}
