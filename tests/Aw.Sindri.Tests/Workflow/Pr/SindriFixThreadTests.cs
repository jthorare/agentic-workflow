using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>A 'sindri.fix:' thread is work for the comment loop even by Sindri's own login (review page spec §8.2); every other Sindri thread stays dropped.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriFixThreadTests
{
    private const string Item = "ITM-0123456789abcdef0123456789abcdef";
    private const string FixMarker = "sindri.fix:" + Item;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Thread(string author, string association, string markers) =>
        $$"""{"key":"thread:1","kind":"thread","author":"{{author}}","association":"{{association}}","isBot":false,"resolved":false,"outdated":false,"path":"a.cs","body":"fix it","markers":{{markers}}}""";

    private static Task<BlockStepResult> Next(string thread) =>
        new PrNextCommentStep().Run(
            BuildShipContexts.Of("step.pr.next-comment", new Dictionary<string, string>(StringComparer.Ordinal) { ["prState"] = $$"""{"comments":[{{thread}}],"self":"sindri-bot"}""" }),
            Token).AsTask();

    private static PullThread Snapshot(string author, params string[] markers) =>
        new("thread:1", PullCommentKind.Thread, author, "MEMBER", IsBot: false, Resolved: false, Outdated: false, "a.cs", "fix it", markers, KnownInstants.Epoch, Line: null, EditedAt: null, Url: null);

    [Fact]
    public async Task A_fix_thread_by_Sindris_login_is_work_and_trusted_as_a_comment()
    {
        var result = await Next(Thread("sindri-bot", "NONE", $"[\"{FixMarker}\"]"));

        Assert.Equal("comment", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_fix_thread_by_another_login_is_work_and_keeps_its_own_trust()
    {
        var member = await Next(Thread("sam-dev", "MEMBER", $"[\"{FixMarker}\"]"));
        var stranger = await Next(Thread("kim-dev", "NONE", $"[\"{FixMarker}\"]"));

        Assert.Equal("comment", member.Result?.Outcome.Value);
        Assert.Equal("untrusted", stranger.Result?.Outcome.Value);
    }

    [Fact]
    public async Task Sindris_login_is_trusted_only_for_fix_threads()
    {
        var plain = await Next(Thread("sindri-bot", "NONE", "[]"));
        var reply = await Next(Thread("sindri-bot", "NONE", "[\"sindri:reply:9\"]"));

        Assert.Equal("drained", plain.Result?.Outcome.Value);
        Assert.Equal("drained", reply.Result?.Outcome.Value);
    }

    [Fact]
    public async Task A_reply_marker_alone_is_dropped_whoever_wrote_it()
    {
        var result = await Next(Thread("sam-dev", "MEMBER", "[\"sindri\"]"));

        Assert.Equal("drained", result.Result?.Outcome.Value);
    }

    [Theory]
    [InlineData("sindri.fix:")]
    [InlineData("sindri.fix:junk")]
    [InlineData("sindri.fix-sent")]
    [InlineData("sindri.fix:ITM-0123")]
    [InlineData("sindri.fix:0123456789abcdefghjkmnpqrs")]
    [InlineData("sindri.fix:01ARZ3NDEKTSV4RRFFQ69G5FAV")]
    [InlineData("sindri.fix:ITM-0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("sindri.fix:01ARZ3NDEKTSV4RRFFQ69G5FA")]
    [InlineData("sindri.fix:01ARZ3NDEKTSV4RRFFQ69G5FAVX")]
    public async Task A_marker_that_only_looks_like_a_fix_marker_is_not_work(string marker)
    {
        var result = await Next(Thread("sam-dev", "MEMBER", $"[\"{marker}\"]"));

        Assert.Equal("drained", result.Result?.Outcome.Value);
        Assert.Null(SindriFixMarker.Item([marker]));
    }

    [Fact]
    public void The_marker_names_an_item_by_id() =>
        Assert.Equal(Item, SindriFixMarker.Item(["sindri:reply:1", "sindri.fix:" + Item]));

    [Fact]
    public async Task The_fact_carries_the_item_so_the_fixed_mark_can_close_it()
    {
        var result = await Next(Thread("sam-dev", "MEMBER", $"[\"{FixMarker}\"]"));

        using var fact = JsonDocument.Parse(result.Result!.Outputs.Single().Value.GetRawText());
        Assert.Equal(Item, fact.RootElement.GetProperty("fixItem").GetString());
    }

    [Theory]
    [InlineData("sam-dev", new string[0], false)]
    [InlineData("sam-dev", new[] { "sindri:reply:9" }, true)]
    [InlineData("sam-dev", new[] { "sindri" }, true)]
    [InlineData("sam-dev", new[] { "sindri.fix:" }, true)]
    [InlineData("sam-dev", new[] { "sindri.fix:" + Item }, false)]
    [InlineData("sindri-bot", new[] { "sindri.fix:" + Item }, false)]
    [InlineData("sam-dev", new[] { "sindri:reply:9", "sindri.fix:" + Item }, false)]
    public async Task The_snapshot_and_the_comment_queue_drop_the_same_threads(string author, string[] markers, bool dropped)
    {
        var json = Thread(author, "MEMBER", JsonSerializer.Serialize(markers));
        var queue = await Next(json);
        var droppedByQueue = queue.Result?.Outcome.Value == "drained";
        var droppedBySnapshot = PrSnapshotStep.Own(Snapshot(author, markers));

        Assert.Equal(dropped, droppedByQueue);
        Assert.Equal(dropped, droppedBySnapshot);
    }

    [Fact]
    public async Task FixItemCloser_closes_the_item_as_done_at_its_version_and_removes_its_task()
    {
        var row = A.Item().WithId(ItemId.From(Item)).AtVersion(4).Build();
        var service = new RecordingClose();
        var files = new FakeFixTaskFiles();

        var closed = await Closer(new FixedItems(row), service, files).Close(Item, Token);

        Assert.True(closed);
        Assert.Equal(new CloseItemRequest(new ItemRef(ItemId.From(Item), Alias: null), CloseReason.Done, ItemVersion: 4), Assert.Single(service.Requests));
        Assert.Equal([ItemId.From(Item)], files.Removed);
    }

    [Fact]
    public async Task FixItemCloser_removes_the_task_of_an_unknown_or_already_closed_item_without_failing()
    {
        var files = new FakeFixTaskFiles();
        var closedRow = A.Item().WithId(ItemId.From(Item)).InState(ItemState.Done, KnownInstants.Epoch).Build();
        var unknown = await Closer(new FixedItems(row: null), new RecordingClose(), files).Close(Item, Token);
        var closedAlready = await Closer(new FixedItems(closedRow), new RecordingClose(), files).Close(Item, Token);
        var junk = await Closer(new FixedItems(row: null), new RecordingClose(), files).Close("not-an-item", Token);

        Assert.False(unknown);
        Assert.False(closedAlready);
        Assert.False(junk);
        Assert.Equal([ItemId.From(Item), ItemId.From(Item)], files.Removed);
    }

    [Fact]
    public async Task FixItemCloser_retries_a_version_conflict_once_and_removes_the_task_when_that_closes()
    {
        var files = new FakeFixTaskFiles();
        var service = new RecordingClose(SindriErrors.ItemConflict.With("changed since you looked"));

        var closed = await Closer(new FixedItems(A.Item().WithId(ItemId.From(Item)).Build()), service, files).Close(Item, Token);

        Assert.True(closed);
        Assert.Equal(2, service.Requests.Count);
        Assert.Equal([ItemId.From(Item)], files.Removed);
    }

    [Fact]
    public async Task FixItemCloser_keeps_the_task_when_the_conflict_comes_twice()
    {
        var files = new FakeFixTaskFiles();
        var service = new RecordingClose(SindriErrors.ItemConflict.With("changed"), SindriErrors.ItemConflict.With("changed again"));

        var closed = await Closer(new FixedItems(A.Item().WithId(ItemId.From(Item)).Build()), service, files).Close(Item, Token);

        Assert.False(closed);
        Assert.Equal(2, service.Requests.Count);
        Assert.Empty(files.Removed);
    }

    [Fact]
    public async Task FixItemCloser_keeps_the_task_on_an_error_that_is_not_a_conflict()
    {
        var files = new FakeFixTaskFiles();
        var service = new RecordingClose(SindriErrors.NotRunning.With("no lease"));

        var closed = await Closer(new FixedItems(A.Item().WithId(ItemId.From(Item)).Build()), service, files).Close(Item, Token);

        Assert.False(closed);
        _ = Assert.Single(service.Requests);
        Assert.Empty(files.Removed);
    }

    private static FixItemCloser Closer(IItemResolver items, RecordingClose service, FakeFixTaskFiles files)
    {
        var callers = new FixedCallers();
        return new FixItemCloser(items, service, files, callers, Microsoft.Extensions.Logging.Abstractions.NullLogger<FixItemCloser>.Instance);
    }

    private sealed class FixedItems(ItemRow? row) : IItemResolver
    {
        public ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken cancellationToken) =>
            ValueTask.FromResult(row is null ? (Result<ItemRow>)SindriErrors.ItemNotFound.With("none") : row);
    }

    private sealed class RecordingClose(params Error[] answers) : IService<CloseItemRequest, CommandAccepted>
    {
        private int _next;

        public List<CloseItemRequest> Requests { get; } = [];

        public ValueTask<Result<CommandAccepted>> Handle(CloseItemRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var answer = _next < answers.Length ? answers[_next++] : null;
            return ValueTask.FromResult(answer is { } error ? (Result<CommandAccepted>)error : new CommandAccepted(A.Caller().Build().CommandId, 1));
        }
    }

    private sealed class FixedCallers : ICallerContextAccessor
    {
        public CallerContext? Current { get; private set; }

        public IDisposable Enter(CallerContext context)
        {
            Current = context;
            return new Leave(this);
        }

        public IDisposable EnterService(string service)
        {
            Current = A.Caller().Service(service).Build();
            return new Leave(this);
        }

        private sealed class Leave(FixedCallers owner) : IDisposable
        {
            public void Dispose() => owner.Current = null;
        }
    }
}
