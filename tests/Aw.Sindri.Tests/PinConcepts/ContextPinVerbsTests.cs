using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>'context pin set|remove' (concept pins spec §3.6, "CLI and agents"): which flags keep, replace or clear a list, what each verb posts, and the usage errors.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPinVerbsTests : IDisposable
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
        await using var services = new ServiceCollection().BuildServiceProvider();
        var exit = await new ContextPinVerbs(client, output, new JobFollower(client, output, TimeProvider.System), services).Run(["pin", .. args, "--detach"], Token, Token);
        return (exit, api);
    }

    private static JsonElement FirstSet(ScriptedApi api) => JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement.GetProperty("set")[0];

    [Fact]
    public async Task An_omitted_list_flag_keeps_the_stored_value_so_nothing_is_sent_for_it()
    {
        var (exit, api) = await Run("set", "Billing", "--summary", "How we take money");

        Assert.Equal(0, exit);
        Assert.Equal("/sindri/context/pins", Assert.Single(api.Calls).Route);
        var set = FirstSet(api);
        Assert.Equal("Billing", set.GetProperty("name").GetString());
        Assert.False(set.TryGetProperty("aliases", out var aliases) && aliases.ValueKind != JsonValueKind.Null);
        Assert.False(set.TryGetProperty("features", out var features) && features.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Alias_flags_replace_the_stored_list_with_the_given_words()
    {
        var (_, api) = await Run("set", "Billing", "--alias", "payments", "--alias", "invoicing", "--feature", "feature:billing.refunds");

        var set = FirstSet(api);
        Assert.Equal("payments|invoicing", string.Join('|', set.GetProperty("aliases").EnumerateArray().Select(static a => a.GetString())));
        Assert.Equal("feature:billing.refunds", string.Join('|', set.GetProperty("features").EnumerateArray().Select(static a => a.GetString())));
    }

    [Fact]
    public async Task Clear_aliases_and_clear_features_send_empty_lists()
    {
        var (_, api) = await Run("set", "Billing", "--clear-aliases", "--clear-features");

        var set = FirstSet(api);
        Assert.Equal((0, 0), (set.GetProperty("aliases").GetArrayLength(), set.GetProperty("features").GetArrayLength()));
    }

    [Fact]
    public async Task Id_and_if_hash_are_sent_as_given()
    {
        var (_, api) = await Run("set", "Billing", "--id", "money", "--if-hash", _hash);

        var root = JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement;
        Assert.Equal(("money", _hash), (root.GetProperty("set")[0].GetProperty("id").GetString(), root.GetProperty("ifHash").GetString()));
    }

    [Fact]
    public async Task From_file_sends_one_pin_per_concept_in_one_request()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aw-pin-list-{RealTime.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "- Billing: How we take money (also: payments)\n- Auth: who you are", Token);
        try
        {
            var (exit, api) = await Run("set", "--from-file", path);

            Assert.Equal(0, exit);
            var set = JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement.GetProperty("set");
            Assert.Equal("Billing|Auth", string.Join('|', set.EnumerateArray().Select(static s => s.GetProperty("name").GetString())));
            Assert.Equal("payments", set[0].GetProperty("aliases")[0].GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_list_line_with_no_name_exits_2_and_posts_nothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aw-pin-list-{RealTime.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "Billing: money\n: no name here", Token);
        try
        {
            var (exit, api) = await Run("set", "--from-file", path);

            Assert.Equal(2, exit);
            Assert.Empty(api.Calls);
            Assert.Contains("line 2 has no name", _error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Remove_sends_the_id_and_passes_with_folder_rules_and_if_hash()
    {
        var (exit, api) = await Run("remove", "billing", "--with-folder-rules", "--if-hash", _hash);

        Assert.Equal(0, exit);
        var root = JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement;
        Assert.Equal(("billing", true, _hash), (string.Join('|', root.GetProperty("remove").EnumerateArray().Select(static r => r.GetString())), root.GetProperty("withFolderRules").GetBoolean(), root.GetProperty("ifHash").GetString()));
    }

    [Fact]
    public async Task Remove_without_with_folder_rules_sends_false()
    {
        var (_, api) = await Run("remove", "billing");

        Assert.False(JsonDocument.Parse(Assert.Single(api.Calls).Body).RootElement.GetProperty("withFolderRules").GetBoolean());
    }

    [Theory]
    [InlineData("set")]   // no name
    [InlineData("set", "A", "B")]   // two names
    [InlineData("set", "Billing", "--alias")]   // a flag without its value
    [InlineData("set", "Billing", "--alias", "x", "--clear-aliases")]   // opposite flags
    [InlineData("set", "Billing", "--if-hash", "not-a-hash")]
    [InlineData("set", "Billing", "--bogus")]
    [InlineData("set", "--from-file", "list.txt", "Billing")]   // a name and a file
    [InlineData("set", "--from-file", "list.txt", "--summary", "x")]
    [InlineData("remove")]
    [InlineData("remove", "a", "b")]
    [InlineData("remove", "billing", "--alias", "x")]   // a set flag on remove
    [InlineData("list", "extra")]
    [InlineData("nope")]
    public async Task Bad_input_exits_2_and_posts_nothing(params string[] args)
    {
        var (exit, api) = await Run(args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public async Task With_detach_it_prints_the_job_id_and_the_success_line_names_the_hash_to_approve()
    {
        var (exit, _) = await Run("set", "Billing");

        Assert.Equal(0, exit);
        Assert.Equal("job JOB-0000000000000000000000000000000e started\n", _out.ToString().ReplaceLineEndings("\n"));
        var done = new Features.PinConcepts.PinConceptsResult([new(Core.Ids.ConceptPinId.From("billing"), "Billing", "created"), new(Core.Ids.ConceptPinId.From("auth"), "Auth", "created"), new(Core.Ids.ConceptPinId.From("old"), "Old", "removed")], Core.Ids.ProfileHash.From(_hash), Approved: false, Dropped: []);
        Assert.Equal($"pinned billing, auth (waiting for your approval: sindri profile approve {_hash})\nunpinned old (waiting for your approval: sindri profile approve {_hash})\n", ContextPinVerbs.Text(done));
    }
}
