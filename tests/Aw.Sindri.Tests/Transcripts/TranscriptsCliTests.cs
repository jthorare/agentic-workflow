using System.Text.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;
using Aw.Transcripts.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Transcripts;

/// <summary>'sindri transcripts list|show' in process over the in-memory corpus: exit 0 ok, 1 an unknown ref, 2 usage; it writes nothing under the home.</summary>
public sealed class TranscriptsCliTests : IDisposable
{
    private static readonly DateTimeOffset _now = StandardCorpus.Start.AddHours(2);
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static SessionRef RefOf(string name) => TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, $"fake/{name}.jsonl");

    [Fact, Trait("Lane", "integration")]
    public async Task List_prints_one_line_per_main_session_newest_first_from_the_last_seven_days()
    {
        var run = await Run(fence: false, "list");

        Assert.Equal(0, run.Exit);
        var lines = run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal([RefOf("fork").Value, RefOf("main").Value], lines.Select(l => l[..23]), StringComparer.Ordinal);
        Assert.Contains("claude-code", lines[1], StringComparison.Ordinal);
        Assert.Contains("2 human turns", lines[1], StringComparison.Ordinal);
        Assert.Contains(StandardCorpus.Repo, lines[1], StringComparison.Ordinal);
        Assert.Equal(string.Empty, run.Stderr);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task List_with_subagents_marks_the_subagent_and_its_parent()
    {
        var run = await Run(fence: false, "list", "--subagents");

        var sub = Assert.Single(run.Stdout.Split('\n'), l => l.StartsWith(RefOf("sub").Value, StringComparison.Ordinal));
        Assert.EndsWith($"subagent of {RefOf("main").Value}", sub, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task List_json_is_one_document_with_the_since_time_and_the_sessions()
    {
        var run = await Run(fence: false, "list", "--since", "30d", "--json");

        Assert.Equal(0, run.Exit);
        using var document = JsonDocument.Parse(run.Stdout);
        var root = document.RootElement;
        Assert.Equal(_now.AddDays(-30), root.GetProperty("since").GetDateTimeOffset());
        var sessions = root.GetProperty("sessions").EnumerateArray().ToList();
        Assert.Equal([RefOf("fork").Value, RefOf("main").Value, RefOf("old").Value], sessions.Select(s => s.GetProperty("ref").GetString()), StringComparer.Ordinal);
        Assert.Equal("claude-code", sessions[1].GetProperty("provider").GetString());
        Assert.Equal(2, sessions[1].GetProperty("humanTurns").GetInt32());
        Assert.False(sessions[1].GetProperty("subagent").GetBoolean());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("2026-10-20", 3)]
    [InlineData("2026-11-01T10:20:00Z", 2)]
    [InlineData("1d", 2)]
    public async Task Since_takes_a_date_a_time_or_a_number_of_days(string since, int expected)
    {
        var run = await Run(fence: false, "list", "--since", since, "--json");

        using var document = JsonDocument.Parse(run.Stdout);
        Assert.Equal(expected, document.RootElement.GetProperty("sessions").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Provider_and_repo_filters_narrow_the_list()
    {
        var codex = await Run(fence: false, "list", "--provider", "codex");
        var other = await Run(fence: false, "list", "--repo", "/work/other");

        Assert.StartsWith("no sessions written since", codex.Stdout, StringComparison.Ordinal);
        Assert.StartsWith("no sessions written since", other.Stdout, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("list --since yesterday")]
    [InlineData("list --since")]
    [InlineData("list --provider copilot")]
    [InlineData("list --repo relative/dir")]
    [InlineData("list --verbose")]
    [InlineData("show")]
    [InlineData("show main.jsonl")]
    [InlineData("tail")]
    public async Task Bad_arguments_are_a_usage_error_with_exit_2(string line)
    {
        var run = await Run(fence: false, line.Split(' '));

        Assert.Equal(2, run.Exit);
        Assert.Contains("SND-CLI-003", run.Stderr, StringComparison.Ordinal);
        Assert.Equal(string.Empty, run.Stdout);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_prints_the_turns_with_roles_tools_and_text()
    {
        var run = await Run(fence: false, "show", RefOf("main").Value);

        Assert.Equal(0, run.Exit);
        Assert.Contains("human  2026-11-01 10:00Z  #2\n  fix the login bug\n", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("assistant  2026-11-01 10:01Z  #3  tools: Read, Edit\n  Looking at it.\n  Fixed.\n", run.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("<untrusted", run.Stdout, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_fences_the_text_for_a_session_and_json_says_so()
    {
        var text = await Run(fence: true, "show", RefOf("main").Value);
        var json = await Run(fence: true, "show", RefOf("main").Value, "--json");

        Assert.Contains("  <untrusted source=\"transcript\">fix the login bug</untrusted>\n", text.Stdout, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json.Stdout);
        Assert.True(document.RootElement.GetProperty("fenced").GetBoolean());
        var turns = document.RootElement.GetProperty("turns").EnumerateArray().ToList();
        Assert.Equal("<untrusted source=\"transcript\">fix the login bug</untrusted>", turns[0].GetProperty("text").GetString());
        Assert.Equal("human", turns[0].GetProperty("role").GetString());
        Assert.Equal($"{RefOf("main").Value}#2", turns[0].GetProperty("ref").GetString());
        Assert.Equal(["Read", "Edit"], turns[1].GetProperty("tools").EnumerateArray().Select(t => t.GetString()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_of_an_unknown_ref_exits_1_with_the_code()
    {
        var run = await Run(fence: false, "show", "transcript:000000000000");

        Assert.Equal(1, run.Exit);
        Assert.StartsWith(TranscriptsErrors.SessionNotFound.Code, run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_person_on_a_terminal_reads_transcripts_unfenced() =>
        Assert.False(TranscriptsCommand.Fenced(AgentSession.Detect(_ => null, interactive: true)));

    [Fact, Trait("Lane", "unit")]
    public void A_session_sindri_launched_reads_transcripts_fenced_even_on_a_terminal() =>
        Assert.True(TranscriptsCommand.Fenced(AgentSession.Detect(name => name == AgentSession.LaunchVariable ? "launch-1" : null, interactive: true)));

    [Fact, Trait("Lane", "unit")]
    public void A_one_off_session_reads_transcripts_fenced_even_on_a_terminal() =>
        Assert.True(TranscriptsCommand.Fenced(AgentSession.Detect(name => name == AgentSession.OneOffVariable ? "1" : null, interactive: true)));

    [Fact, Trait("Lane", "unit")]
    public void A_caller_without_a_terminal_reads_transcripts_fenced() =>
        Assert.True(TranscriptsCommand.Fenced(AgentSession.Detect(_ => null, interactive: false)));

    [Fact, Trait("Lane", "integration")]
    public async Task Show_escapes_control_bytes_in_turn_text_and_keeps_newlines()
    {
        var session = TranscriptRefs.ForFile(TranscriptProvider.ClaudeCode, "fake/ansi.jsonl");
        var turn = new TranscriptTurn(TranscriptRefs.ForTurn(session, 1), TurnRole.Human, StandardCorpus.Start, "red \u001b[31mtext\u001b]52;c;Zm9v\u0007\nnext\tline", []);
        var transcript = new SessionTranscript(new TranscriptSession(session, TranscriptProvider.ClaudeCode, StandardCorpus.Start, StandardCorpus.Start, Repo: null, IsSubagent: false, Parent: null, HumanTurns: 1), [turn], 0);

        var run = await Run(new InMemoryTranscriptCorpus([transcript]), fence: false, "show", session.Value);

        Assert.Contains("  red \\x1b[31mtext\\x1b]52;c;Zm9v\\x07\n  next\tline\n", run.Stdout, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', run.Stdout);
    }

    private Task<CliRun> Run(bool fence, params string[] args) =>
        Run(new InMemoryTranscriptCorpus(StandardCorpus.Transcripts(RefOf)), fence, args);

    private async Task<CliRun> Run(InMemoryTranscriptCorpus corpus, bool fence, params string[] args)
    {
        var collection = Composition.Compose(new ServiceCollection(), name => name == "AW_SINDRI_HOME" ? _home.Path : null, new FakeTimeProvider(_now));
        _ = collection.AddSingleton<ITranscriptCorpus>(corpus);
        var provider = collection.BuildServiceProvider();
        await using var disposeProvider = provider;
        await using var scope = provider.CreateAsyncScope();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var output = new Output(stdout, stderr, scope.ServiceProvider.GetRequiredService<JsonSerializerOptions>());

        var exit = await TranscriptsCommand.Run(args, scope.ServiceProvider, output, fence, TestContext.Current.CancellationToken);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_home.Path));
        return new CliRun(exit, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliRun(int Exit, string Stdout, string Stderr);
}
