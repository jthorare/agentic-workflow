using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>Sending a finding as a fix task (PR review page spec §8.2): a fix-task file that can't be written is a half-done answer with the item, never a bare failure, and a retry writes the file without a second thread.</summary>
[Lane(TestLane.Unit)]
public sealed class SendReviewFixServiceTests
{
    private const string Owner = "joi-example";
    private static readonly string _head = new('a', 40);
    private readonly SliceHarness _h = new();
    private readonly FakePullRequests _forge = new(["github.com"]) { SelfLogin = "sindri-bot" };
    private readonly FakePull _pull;
    private readonly FakeFixTaskFiles _files = new();
    private readonly FlakyFiles _flaky;
    private readonly Items _items = new();
    private readonly SendReviewFixService _service;

    public SendReviewFixServiceTests()
    {
        _flaky = new FlakyFiles(_files);
        _pull = _forge.Add(new PullRepo("github.com", "acme", "api"), 1, "feat/retry", _head);
        _pull.Author = Owner;
        _pull.Files = [new PullFile("src/Sync.cs", PreviousPath: null, PullFileStatus.Modified, 3, 0, "@@ -40,0 +41,3 @@\n+a\n+b\n+c")];
        var facts = new FakeReviewFacts().With(new ReviewFactsSnapshot([Pull()], Github: null, new ReviewLibraryFacts(Reading: false, Docs: 0, WithErrors: 0, [])));
        var targets = new FixTargets(_h.Profiles, new Repos(), facts, _forge, NullLogger<FixTargets>.Instance);
        _service = new SendReviewFixService(_h.Callers, targets, _items, new FixThreadPoster(_forge), new FixRoute(new Babysitters(), new Sessions()), _flaky, new FixSentRecorder(_h.Held, _h.Store, _h.Events));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPull Pull() =>
        new("acme/api", 1, "Add retry budget", Owner, Draft: false, new DateTimeOffset(2025, 12, 29, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 12, 31, 12, 0, 0, TimeSpan.Zero), _head, [], [], ReviewersRead: true, [], Url: "https://github.com/acme/api/pull/1");

    private async Task<Result<SendReviewFixResponse>> Send(string? item = null)
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Review: new PrReviewSettings(Owner))).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
        _ = await _h.HoldLease();
        using var caller = _h.Callers.Enter(new CallerContext(CallerClass.Human, Terminal: false, "human:test", _h.Ids.New<CommandId>(), _h.Ids.New<CorrelationId>(), Repo: null));
        return await _service.Handle(new SendReviewFixRequest("acme", "api", 1, "line", "Guard the null.", _head, Source: null, "src/Sync.cs", 42, Side: null, item), Token);
    }

    private int Sent() => _h.Store.Events.Count(static e => e.Envelope.Type == EventTypes.ReviewFixSent);

    [Fact]
    public async Task A_task_file_that_cannot_be_written_still_answers_with_the_item_and_records_the_send()
    {
        _flaky.Fails = true;

        var sent = await Send();

        Assert.True(sent.IsSuccess);
        Assert.Equal(_items.Made, sent.Value.ItemId);
        Assert.True(sent.Value.Posted);
        Assert.Contains("fix-task", sent.Value.FileError, StringComparison.Ordinal);
        Assert.Equal(1, Sent());
        _ = Assert.Single(_pull.Threads);
        Assert.Empty(_files.Tasks);
    }

    [Fact]
    public async Task A_retry_with_the_item_writes_the_missing_file_without_a_second_thread_or_item()
    {
        _flaky.Fails = true;
        var first = (await Send()).Value;
        _flaky.Fails = false;

        var retry = await Send(first.ItemId.Value);

        Assert.True(retry.IsSuccess);
        Assert.Null(retry.Value.FileError);
        Assert.Equal(first.ItemId, retry.Value.ItemId);
        Assert.Equal(1, _items.Creates);
        _ = Assert.Single(_pull.Threads);
        Assert.Equal([("acme/api:feat/retry", first.ItemId, 1)], _files.Tasks);
    }

    private sealed class FlakyFiles(FakeFixTaskFiles inner) : IFixTaskFiles
    {
        public bool Fails { get; set; }

        public ValueTask<Result<bool>> Add(string head, ItemId item, int pr, CancellationToken cancellationToken) =>
            Fails ? ValueTask.FromResult<Result<bool>>(SindriErrors.ServiceFailed.With("the fix-task files can't be written: disk full")) : inner.Add(head, item, pr, cancellationToken);

        public ValueTask<Result<bool>> Remove(ItemId item, CancellationToken cancellationToken) => inner.Remove(item, cancellationToken);
    }

    private sealed class Items : IFixItems
    {
        public int Creates { get; private set; }

        public ItemId Made { get; } = ItemId.From("ITM-" + new string('c', 32));

        public ValueTask<Result<ItemId>> Create(string title, RepoName? repo, string body, CancellationToken cancellationToken)
        {
            Creates++;
            return ValueTask.FromResult<Result<ItemId>>(Made);
        }
    }

    private sealed class Repos : IReviewRepos
    {
        private static readonly string[] _synced = ["acme/api"];

        public ValueTask<Result<IReadOnlyList<string>>> Synced(CancellationToken cancellationToken) => ValueTask.FromResult<Result<IReadOnlyList<string>>>(_synced);
    }

    private sealed class Babysitters : IFixBabysitters
    {
        public ValueTask<bool> Watching(string link, CancellationToken cancellationToken) => ValueTask.FromResult(false);
    }

    private sealed class Sessions : IFixSessions
    {
        public ValueTask<string?> OnBranch(string headRepo, string branch, CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);
    }
}
