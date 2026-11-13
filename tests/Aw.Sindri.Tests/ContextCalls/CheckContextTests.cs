using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

[Lane(TestLane.Unit)]
public sealed class CheckContextTests
{
    private const string Marker = "personal-project-marker";

    [Theory]
    [InlineData(ProviderFile.Claude, """{"mcpServers":{"sindri":{"type":"http"}},"projects":{"/x":{"note":"personal-project-marker"}}}""")]
    [InlineData(ProviderFile.Codex, "model = \"x\" # personal-project-marker\n\n[mcp_servers.sindri]\nurl = \"http://127.0.0.1:7410/sindri/mcp\"\n")]
    [InlineData(ProviderFile.Codex, "[mcp_servers.\"sindri\".env]\nX = \"personal-project-marker\"\n")]
    [InlineData(ProviderFile.Codex, "[mcp_servers]\nother = { url = \"personal-project-marker\" }\nsindri = { url = \"x\" }\n")]
    [InlineData(ProviderFile.Codex, "mcp_servers.sindri.url = \"x\" # personal-project-marker\n")]
    [InlineData(ProviderFile.Codex, "mcp_servers = { other = {}, 'sindri' = { url = \"personal-project-marker\" } }\n")]
    [InlineData(ProviderFile.Cursor, """{"mcpServers":{"sindri":{"url":"x"},"personal-project-marker":{}}}""")]
    public void A_user_wide_sindri_entry_is_026_naming_only_the_file(ProviderFile file, string text)
    {
        var check = Check(new FakeProviderFiles().With(file, text));

        Assert.Equal((ContextChecks.McpScopeName, CheckState.Warn), (check.Name, check.State));
        Assert.StartsWith("SND-CONTEXT-026", check.Detail, StringComparison.Ordinal);
        Assert.Contains(ProviderFilesContract.Shown(file), check.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(Marker, check.Detail + check.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProviderFile.Claude, """{"mcpServers":{"prism":{}},"projects":{"/work/demo":{"mcpServers":{"sindri":{"type":"http"}}}}}""")]
    [InlineData(ProviderFile.Codex, "[mcp_servers.sindri-ownercheck]\n[mcp_servers.other]\n")]
    [InlineData(ProviderFile.Codex, "# [mcp_servers.sindri]\nnote = \"[mcp_servers.sindri] # not a table\"\n[profiles.sindri]\nsindri = 1\n")]
    [InlineData(ProviderFile.Cursor, """{"mcpServers":{}}""")]
    public void Local_scope_other_servers_and_missing_files_are_ok(ProviderFile file, string text)
    {
        Assert.Equal(CheckState.Ok, Check(new FakeProviderFiles().With(file, text)).State);
        Assert.Equal(CheckState.Ok, Check(new FakeProviderFiles()).State);
    }

    [Fact]
    public void A_file_that_cant_be_read_or_parsed_is_a_warning_naming_it()
    {
        var malformed = Check(new FakeProviderFiles().With(ProviderFile.Claude, "{ \"personal-project-marker\": "));
        var denied = Check(new FakeProviderFiles().Unreadable(ProviderFile.Cursor));

        Assert.Equal((CheckState.Warn, "couldn't check ~/.claude.json (not valid JSON)"), (malformed.State, malformed.Detail));
        Assert.Equal((CheckState.Warn, "couldn't check ~/.cursor/mcp.json (permission denied)"), (denied.State, denied.Detail));
    }

    [Fact]
    public void Sources_are_ok_warn_or_fail_by_their_states_and_the_session_switch()
    {
        var ok = Row(SourceState.Ok, code: null);
        var down = Row(SourceState.Unavailable, "SND-CONTEXT-010");

        Assert.Equal(CheckState.Ok, ContextChecks.Sources(View(sessions: true, ok)).State);
        Assert.Equal(CheckState.Ok, ContextChecks.Sources(View(sessions: false, ok)).State);
        Assert.Contains("SND-CONTEXT-020", ContextChecks.Sources(View(sessions: false, ok)).Detail, StringComparison.Ordinal);
        Assert.Equal(CheckState.Warn, ContextChecks.Sources(View(sessions: true, ok, down)).State);
        Assert.StartsWith("SND-CONTEXT-011", ContextChecks.Sources(View(sessions: true, down)).Detail, StringComparison.Ordinal);
        Assert.Equal(CheckState.Fail, ContextChecks.Sources(View(sessions: true, down)).State);
        Assert.Equal(CheckState.Warn, ContextChecks.Sources(View(sessions: true, Row(SourceState.Degraded, code: null))).State);   // why: never refreshed is degraded, not 011
    }

    [Fact]
    public async Task The_checks_read_the_live_profile_and_every_provider_file()
    {
        var harness = new SliceHarness();
        var checks = new ContextDoctorChecks(new ContextScopes(harness.Profiles, harness.Callers, new FakeRepoCheckouts()), FakeContextEngine.Instance,
            new FakeProviderFiles().With(ProviderFile.Codex, "[mcp_servers.sindri]\n"));

        var none = await checks.Run(TestContext.Current.CancellationToken);

        Assert.Equal([ContextChecks.SourcesName, ContextChecks.SyncName, ContextChecks.McpScopeName], none.Select(c => c.Name), StringComparer.Ordinal);
        Assert.StartsWith("SND-PROFILE-001", none[0].Detail, StringComparison.Ordinal);   // why: no profile yet
        Assert.Equal(CheckState.Warn, none[2].State);
    }

    [Fact]
    public async Task An_approved_profile_without_a_context_section_is_ok_and_says_so()
    {
        var harness = new SliceHarness();
        _ = await harness.Profiles.Save(SliceHarness.Profile(), TestContext.Current.CancellationToken);
        var checks = new ContextDoctorChecks(new ContextScopes(harness.Profiles, harness.Callers, new FakeRepoCheckouts()), FakeContextEngine.Instance, new FakeProviderFiles());

        var rows = await checks.Run(TestContext.Current.CancellationToken);

        Assert.Equal((CheckState.Ok, "no context: section in the profile, so no context sources"), (rows[0].State, rows[0].Detail));
        Assert.Equal(CheckState.Ok, rows[1].State);
    }

    private static DoctorCheck Check(FakeProviderFiles files) =>
        ContextChecks.McpScope([.. Enum.GetValues<ProviderFile>().Select(f => McpScope.Judge(files.Read(f, CancellationToken.None).AsTask().Result)).OfType<McpScopeFinding>()]);

    private static SourceStatusRow Row(SourceState state, string? code) =>
        new(SourceId.From("repo-docs"), SourceKind.Doc, Repo: null, state, "checked", code, LastSync: null, Records: 3);

    private static Result<SourcesView> View(bool sessions, params SourceStatusRow[] rows) => new SourcesView(rows, sessions, DateTimeOffset.UnixEpoch);
}
