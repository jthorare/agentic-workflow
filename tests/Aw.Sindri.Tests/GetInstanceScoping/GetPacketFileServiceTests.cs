using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.GetInstanceScoping;

/// <summary>'GetPacketFile' (scoping spec API9): one file of the candidate packet by hash, hashed again on each read.</summary>
[Lane(TestLane.Unit)]
public sealed class GetPacketFileServiceTests
{
    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly string _hash = "sha256:" + new string('a', 64);

    private sealed class Contents(string packetFact) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(packetFact)));
    }

    private sealed class Store(string hash, string? text) : IScopePacketStore
    {
        public int Reads { get; private set; }

        public ValueTask<Result<string>> Hash(WorkflowInstanceId instance, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(hash);

        public ValueTask<Result<string>> ReadFile(WorkflowInstanceId instance, string name, CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult<Result<string>>(text is null ? SindriErrors.ItemNotFound.With("no such file") : text);
        }
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    private static async Task<(Result<PacketFile> Result, Store Store)> Read(string name, string asked, string computed, string? text = "{\"schemaVersion\":1}", string recipe = "workflow.scoping", CallerContext? caller = null)
    {
        var view = StatusCases.View("desk-task");
        view = view with { ItemId = ItemKey.From(Item), Recipe = view.Recipe with { Id = RecipeId.From(recipe) }, FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:scopePacket"] = new string('1', 64) } };
        var directory = new SomeInstances(view);
        var store = new Store(computed, text);
        var opened = new ScopingInstances(directory, directory, new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build()));
        var service = new GetPacketFileService(opened, new Contents($$"""{"hash":"{{_hash}}"}"""), store, new Redacting());
        return (await service.Handle(new GetPacketFileRequest(view.Id, asked, name), CancellationToken.None), store);
    }

    [Fact]
    public async Task The_current_packets_file_is_served_scrubbed_with_its_format()
    {
        var (result, _) = await Read("coverage.json", _hash, _hash, text: "{\"note\":\"sk-live-123\"}");

        Assert.Equal(("coverage.json", "{\"note\":\"[redacted]\"}", "json"), (result.Value.Name, result.Value.Text, result.Value.Format));
    }

    [Theory]
    [InlineData("answers.jsonl", "jsonl")]
    [InlineData("rounds.jsonl", "jsonl")]
    [InlineData("brief.md", "md")]
    [InlineData("lenses.json", "json")]
    public async Task Every_file_the_packet_lists_is_served_in_its_format(string name, string format)
    {
        var (result, _) = await Read(name, _hash, _hash);

        Assert.Equal(format, result.Value.Format);
    }

    [Theory]
    [InlineData("../acceptance.json")]
    [InlineData("scope-packet/brief.md")]
    [InlineData("packet.json")]
    [InlineData("")]
    public async Task A_name_that_is_not_a_packet_file_is_item_404_and_nothing_is_read(string name)
    {
        var (result, store) = await Read(name, _hash, _hash);

        Assert.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task A_hash_that_is_not_the_instances_current_candidate_is_item_404()
    {
        var (result, _) = await Read("brief.md", "sha256:" + new string('b', 64), _hash);

        Assert.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_directory_that_hashes_to_something_else_now_is_SND_SCOPING_007_with_its_fix()
    {
        var (result, store) = await Read("brief.md", _hash, "sha256:" + new string('c', 64));

        Assert.Equal(ScopingErrors.PacketChanged.Code, result.Error.Code);
        Assert.Contains("(fix: ", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task A_file_the_run_area_lacks_is_item_404()
    {
        var (result, _) = await Read("brief.md", _hash, _hash, text: null);

        Assert.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task An_instance_of_another_recipe_is_SND_SCOPING_001()
    {
        var (result, _) = await Read("brief.md", _hash, _hash, recipe: "workflow.project");

        Assert.Equal(ScopingErrors.NotAScopingInstance.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_of_any_repo_reads_the_file()
    {
        var (own, _) = await Read("brief.md", _hash, _hash, caller: new CallerContextBuilder().Session().InRepo("alpha").Build());
        var (other, _) = await Read("brief.md", _hash, _hash, caller: new CallerContextBuilder().Session().InRepo("beta").Build());

        Assert.True(own.IsSuccess);
        Assert.True(other.IsSuccess);
    }
}
