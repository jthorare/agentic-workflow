using Aw.Kernel;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The memory calls of the fake: in memory, seedable, each call recorded.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly Dictionary<string, (MemoryView Store, List<MemoryFactView> Facts)> _memories = new(StringComparer.Ordinal);
    private readonly List<(string Name, string Query, string? Level, int? Limit)> _searches = [];
    private readonly List<(string Name, string? FactId, string? Tag, bool Expired)> _forgets = [];
    private readonly List<(string Name, WriteMemoryFactBody Body)> _writtenFacts = [];
    private readonly List<CreateMemoryBody> _createdMemories = [];
    private static readonly MemoryProvenance _noProvenance = new(Workflow: null, Run: null, Item: null, Session: null);
    private int _factSerial;

    /// <summary>What "now" is for expiry: ForgetMemoryFacts with expired set forgets the facts that expire at or before it.</summary>
    public DateTimeOffset ForgetNow { get; set; } = DateTimeOffset.UnixEpoch.AddDays(1);

    /// <summary>The mode SearchMemory answers with ('semantic' or 'keyword').</summary>
    public string SearchMode { get; set; } = "semantic";

    /// <summary>The note SearchMemory answers with (why it fell back to keyword), or null.</summary>
    public string? SearchNote { get; set; }

    /// <summary>When set, ForgetMemoryFacts answers this error and deletes nothing.</summary>
    public Error? ForgetError { get; set; }

    /// <summary>When set, CreateMemory and WriteMemoryFact answer this error.</summary>
    public Error? MemoryWriteError { get; set; }

    /// <summary>Every search made, in order.</summary>
    public IReadOnlyList<(string Name, string Query, string? Level, int? Limit)> Searches { get { lock (_gate) { return [.. _searches]; } } }

    /// <summary>Every forget sent, in order.</summary>
    public IReadOnlyList<(string Name, string? FactId, string? Tag, bool Expired)> Forgets { get { lock (_gate) { return [.. _forgets]; } } }

    /// <summary>Every fact write sent, in order.</summary>
    public IReadOnlyList<(string Name, WriteMemoryFactBody Body)> WrittenFacts { get { lock (_gate) { return [.. _writtenFacts]; } } }

    /// <summary>Every create sent, in order.</summary>
    public IReadOnlyList<CreateMemoryBody> CreatedMemories { get { lock (_gate) { return [.. _createdMemories]; } } }

    /// <summary>Makes a memory store exist with these facts.</summary>
    /// <param name="name">The store.</param>
    /// <param name="facts">Its facts.</param>
    /// <returns>The store's row.</returns>
    public MemoryView SeedMemory(string name, params MemoryFactView[] facts)
    {
        lock (_gate)
        {
            var store = new MemoryView(name, Description: null, "owner", DateTimeOffset.UnixEpoch, facts.Length);
            _memories[name] = (store, [.. facts]);
            return store;
        }
    }

    /// <summary>A fact for a test.</summary>
    /// <param name="key">The key.</param>
    /// <param name="text">The text.</param>
    /// <param name="tags">The tags.</param>
    /// <param name="expiresAt">When it expires, or null.</param>
    /// <returns>The fact.</returns>
    public static MemoryFactView FactRow(string key, string text, string[]? tags = null, DateTimeOffset? expiresAt = null) =>
        new(key, text, tags ?? [], _noProvenance, expiresAt, DateTimeOffset.UnixEpoch, Score: null);

    public Task<Result<MemoryList>> ListMemories(Uri api, CancellationToken cancellationToken)
    {
        if (NotReceived<MemoryList>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            return Task.FromResult<Result<MemoryList>>(new MemoryList([.. _memories.Values.Select(m => m.Store with { Facts = m.Facts.Count }).OrderBy(m => m.Name, StringComparer.Ordinal)]));
        }
    }

    public Task<Result<MemoryCreated>> CreateMemory(Uri api, CreateMemoryBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (NotReceived<MemoryCreated>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _createdMemories.Add(body);
            if (MemoryWriteError is { } error)
            {
                return Task.FromResult<Result<MemoryCreated>>(error);
            }

            var store = new MemoryView(body.Name, body.Description, "owner", DateTimeOffset.UnixEpoch, 0);
            _memories[body.Name] = (store, []);
            return Task.FromResult<Result<MemoryCreated>>(new MemoryCreated(store));
        }
    }

    public Task<Result<MemorySearchView>> SearchMemory(Uri api, MemorySearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        var (name, query, level, limit) = search;
        if (NotReceived<MemorySearchView>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _searches.Add((name, query, level, limit));
            if (!_memories.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<MemorySearchView>>(NoMemory(name));
            }

            var hits = found.Facts
                .Where(f => f.Text.Contains(query, StringComparison.OrdinalIgnoreCase) || f.Tags.Contains(query, StringComparer.OrdinalIgnoreCase))
                .Take(limit ?? int.MaxValue)
                .Select(f => f with { Score = 1f })
                .ToList();
            return Task.FromResult<Result<MemorySearchView>>(new MemorySearchView(hits, hits.Count, SearchMode, SearchNote));
        }
    }

    public Task<Result<MemoryFactWritten>> WriteMemoryFact(Uri api, string name, WriteMemoryFactBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (NotReceived<MemoryFactWritten>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _writtenFacts.Add((name, body));
            if (MemoryWriteError is { } error)
            {
                return Task.FromResult<Result<MemoryFactWritten>>(error);
            }

            if (!_memories.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<MemoryFactWritten>>(NoMemory(name));
            }

            var same = found.Facts.FindIndex(f => string.Equals(f.Text, body.Text, StringComparison.Ordinal));
            var key = same >= 0 ? found.Facts[same].Key : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"fact-{++_factSerial}");
            var fact = new MemoryFactView(key, body.Text, body.Tags ?? [], body.Provenance ?? _noProvenance, body.ExpiresAt, DateTimeOffset.UnixEpoch, Score: null);
            if (same >= 0)
            {
                found.Facts[same] = fact;
            }
            else
            {
                found.Facts.Add(fact);
            }

            return Task.FromResult<Result<MemoryFactWritten>>(new MemoryFactWritten(key, same >= 0 ? "refreshed" : "added"));
        }
    }

    public Task<Result<MemoryForgotten>> ForgetMemoryFacts(Uri api, MemoryForget forget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(forget);
        var (name, factId, tag, expired) = forget;
        if (NotReceived<MemoryForgotten>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _forgets.Add((name, factId, tag, expired));
            if (ForgetError is { } error)
            {
                return Task.FromResult<Result<MemoryForgotten>>(error);
            }

            if (!_memories.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<MemoryForgotten>>(NoMemory(name));
            }

            var now = ForgetNow;
            var gone = found.Facts.RemoveAll(f =>
                (factId is not null && f.Key == factId)
                || (tag is not null && f.Tags.Contains(tag, StringComparer.Ordinal))
                || (expired && f.ExpiresAt is { } at && at <= now));
            return Task.FromResult<Result<MemoryForgotten>>(new MemoryForgotten(gone));
        }
    }

    private static Error NoMemory(string name) => new("SND-MEM-404", ErrorKind.NotFound, $"No memory named {name}.");
}
