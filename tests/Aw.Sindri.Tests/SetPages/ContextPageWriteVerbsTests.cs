using System.Net;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.SetPages;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.SetPages;

/// <summary>'context page set|remove' (pages spec §3.6, "CLI"): what each verb posts, how '--action' reads, '--from-file', the usage errors and the lines it prints.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPageWriteVerbsTests : IDisposable
{
    private const string Started = """{"accepted":{"commandId":"00000000-0000-0000-0000-00000000000c","sequence":5},"jobId":"JOB-0000000000000000000000000000000e","links":{"job":"/sindri/jobs/JOB-0000000000000000000000000000000e","cancel":"/sindri/jobs/JOB-0000000000000000000000000000000e/cancel","events":"/sindri/events?after=4&types=job.*"}}""";
    private static readonly string _hash = new('a', 64);
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(int Exit, ScriptedApi Api)> Run(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, Started));
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        await using var services = new ServiceCollection().AddSingleton<IProfileTextParser>(new YamlProfileTextParser(SliceHarness.Json)).BuildServiceProvider();
        var exit = await new ContextPageWriteVerbs(client, output, new JobFollower(client, output, TimeProvider.System), services).Run(["page", .. args, "--detach"], Token, Token);
        return (exit, api);
    }

    private static JsonElement Body(ScriptedApi api) => JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement;

    [Fact]
    public async Task Set_posts_one_page_with_the_flags_as_given()
    {
        var (exit, api) = await Run(
            "set", "Schedule week", "--id", "week", "--repo", "web", "--surface", "surface:route/GET:/schedule/week", "--if-hash", _hash, "--dry-run",
            "--action", "Submit a schedule=action:SchedulePage.submit-schedule->self-scheduling, approval-workflow", "--action", "Cancel");

        Assert.Equal(0, exit);
        Assert.Equal("/sindri/context/pages", Assert.Single(api.Calls).Route);
        var root = Body(api);
        Assert.Equal((_hash, true), (root.GetProperty("ifHash").GetString(), root.GetProperty("dryRun").GetBoolean()));
        var page = root.GetProperty("set")[0];
        Assert.Equal(("Schedule week", "week", "web", "surface:route/GET:/schedule/week"), (page.GetProperty("name").GetString(), page.GetProperty("id").GetString(), page.GetProperty("repo").GetString(), page.GetProperty("surface").GetString()));
        var submit = page.GetProperty("actions")[0];
        Assert.Equal(("Submit a schedule", "action:SchedulePage.submit-schedule"), (submit.GetProperty("name").GetString(), submit.GetProperty("action").GetString()));
        Assert.Equal("self-scheduling|approval-workflow", string.Join('|', submit.GetProperty("concepts").EnumerateArray().Select(static c => c.GetString())));
        Assert.Equal("Cancel", page.GetProperty("actions")[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_page_with_no_actions_sends_none_and_no_dry_run()
    {
        var (_, api) = await Run("set", "Week");

        var root = Body(api);
        Assert.False(root.GetProperty("dryRun").GetBoolean());
        Assert.False(root.GetProperty("set")[0].TryGetProperty("actions", out var actions) && actions.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Remove_sends_the_id_and_the_if_hash()
    {
        var (exit, api) = await Run("remove", "schedule-week", "--if-hash", _hash);

        Assert.Equal(0, exit);
        var root = Body(api);
        Assert.Equal("schedule-week", string.Join('|', root.GetProperty("remove").EnumerateArray().Select(static r => r.GetString())));
        Assert.Equal(_hash, root.GetProperty("ifHash").GetString());
    }

    [Theory]
    [InlineData("Submit->", "no concept after '->'")]
    [InlineData("Submit->a,,b", "no concept after '->'")]
    [InlineData("Submit=", "no key after '='")]
    [InlineData("Submit=->a", "no key after '='")]
    [InlineData("->a", "no name before '=' or '->'")]
    [InlineData("=action:X.y", "no name before '=' or '->'")]
    [InlineData("A,B", "a name has no ',' in it")]
    public async Task A_bad_action_spec_is_256_naming_the_value_and_posts_nothing(string spec, string problem)
    {
        var (exit, api) = await Run("set", "Week", "--action", spec);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
        Assert.Contains(ContextErrors.PageActionSpecInvalid.Code, _error.ToString(), StringComparison.Ordinal);
        Assert.Contains($"--action \"{spec}\": {problem}", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Submit a schedule", "Submit a schedule", null, null)]
    [InlineData("Submit=action:A.b", "Submit", "action:A.b", null)]
    [InlineData("Submit->x", "Submit", null, "x")]
    [InlineData(" Submit = action:A.b -> x , y ", "Submit", "action:A.b", "x|y")]
    public void An_action_spec_reads_name_key_and_concepts(string spec, string name, string? key, string? concepts)
    {
        var action = ContextPageArguments.Action(spec);

        Assert.True(action.IsSuccess);
        Assert.Equal((name, key, concepts), (action.Value.Name, action.Value.Action, action.Value.Concepts is null ? null : string.Join('|', action.Value.Concepts)));
    }

    [Theory]
    [InlineData("set")]
    [InlineData("set", "One", "Two")]
    [InlineData("set", "Week", "--from-file", "x.yaml")]
    [InlineData("set", "--from-file", "x.yaml", "--surface", "surface:a")]
    [InlineData("set", "Week", "--id", "BAD ID")]
    [InlineData("set", "Week", "--if-hash", "nothex")]
    [InlineData("set", "Week", "--nope")]
    [InlineData("remove")]
    [InlineData("remove", "a", "b")]
    [InlineData("remove", "a", "--dry-run")]
    [InlineData("remove", "a", "--action", "X")]
    public async Task A_line_that_does_not_fit_is_exit_2_and_posts_nothing(params string[] args)
    {
        var (exit, api) = await Run(args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public async Task From_file_sends_every_page_of_an_index_section()
    {
        var path = await Write("index:\n  pages:\n    - name: Schedule week\n      surface: 'surface:route/GET:/schedule/week'\n      actions:\n        - name: Submit\n          concepts: [self-scheduling]\n    - id: queue\n      name: Queue\n");
        try
        {
            var (exit, api) = await Run("set", "--from-file", path, "--dry-run");

            Assert.Equal(0, exit);
            var set = Body(api).GetProperty("set");
            Assert.Equal("Schedule week|Queue", string.Join('|', set.EnumerateArray().Select(static p => p.GetProperty("name").GetString())));
            Assert.Equal("queue", set[1].GetProperty("id").GetString());
            Assert.Equal("self-scheduling", set[0].GetProperty("actions")[0].GetProperty("concepts")[0].GetString());
            Assert.True(Body(api).GetProperty("dryRun").GetBoolean());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task From_file_takes_a_bare_pages_list_too()
    {
        var path = await Write("pages:\n  - name: Queue\n");
        try
        {
            var (exit, api) = await Run("set", "--from-file", path);

            Assert.Equal(0, exit);
            Assert.Equal("Queue", Body(api).GetProperty("set")[0].GetProperty("name").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("---\npages:\n  - name: Queue\n")]
    [InlineData("# the pages\n\npages:\n  - name: Queue\n")]
    [InlineData("---\n# the pages\nindex:\n  pages:\n    - name: Queue\n")]
    public async Task From_file_reads_a_leading_document_marker_or_comments(string text)
    {
        var path = await Write(text);
        try
        {
            var (exit, api) = await Run("set", "--from-file", path);

            Assert.Equal(0, exit);
            Assert.Equal("Queue", Body(api).GetProperty("set")[0].GetProperty("name").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("pages: []\n", "has no pages")]
    [InlineData("pages:\n  - name: Q\n    colour: red\n", "colour")]
    public async Task From_file_with_nothing_or_a_typo_is_exit_2_naming_it(string text, string expected)
    {
        var path = await Write(text);
        try
        {
            var (exit, api) = await Run("set", "--from-file", path);

            Assert.Equal(2, exit);
            Assert.Empty(api.Calls);
            Assert.Contains(expected, _error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_missing_file_is_exit_2()
    {
        var (exit, api) = await Run("set", "--from-file", "/nonexistent/aw-pages.yaml");

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void A_written_result_prints_a_line_per_page_and_the_hash_to_approve()
    {
        var done = new SetPagesResult([new PageWritten(PagePinId.From("week"), "Week", Repo: null, "created"), new PageWritten(PagePinId.From("old"), "Old", Repo: null, "removed")], ProfileHash.From(_hash), Written: true, Approved: false, ["index.pages.week: (none) -> Week"]);

        Assert.Equal($"created week (Week)\nremoved old (Old)\nwaiting for your approval: sindri profile approve {_hash}\n", ContextPageWriteVerbs.Text(done).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void A_dry_run_prints_the_rows_and_that_nothing_was_written()
    {
        var done = new SetPagesResult([new PageWritten(PagePinId.From("week"), "Week", Repo: null, "created")], ProfileHash.From(_hash), Written: false, Approved: false, ["index.pages.week: (none) -> Week", "repos.web.index.pages.q: Q -> (none)"]);

        Assert.Equal("index.pages.week: (none) -> Week\nrepos.web.index.pages.q: Q -> (none)\ndry run: nothing written\n", ContextPageWriteVerbs.Text(done).ReplaceLineEndings("\n"));
    }

    private static async Task<string> Write(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aw-pages-{RealTime.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(path, text, Token);
        return path;
    }
}
