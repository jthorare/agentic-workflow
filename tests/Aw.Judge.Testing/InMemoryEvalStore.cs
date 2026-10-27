using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>
/// The in-memory IEvalStore fake; it passes EvalStoreContract. The decision log's side of the reads is seeded with
/// <see cref="SeedDecision"/>, because the fake has no decision log behind it.
/// </summary>
/// <param name="redactor">Redacts an item's input before it is stored; the contract's samples when none is given.</param>
public sealed class InMemoryEvalStore(IInputRedactor? redactor = null) : IEvalStore
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the in-memory eval store is set unavailable");
    private static readonly LabelSource[] _strongestFirst = [LabelSource.Override, LabelSource.Outcome, LabelSource.Adjudicator];

    private readonly IInputRedactor _redactor = redactor ?? new FakeInputRedactor();
    private readonly List<EvalItem> _items = [];
    private readonly Dictionary<(string Item, LabelSource Source), string> _labels = [];
    private readonly List<(DecidedInput Input, DateTimeOffset At, SessionId? Session)> _decided = [];

    /// <summary>When true every call returns an error, as a store that can't be opened.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The items added so far, in insertion order.</summary>
    public IReadOnlyList<EvalItem> Items => _items;

    /// <summary>Makes a decided decision with details visible to <see cref="DecidedSince"/> and <see cref="OutcomeCandidates"/>.</summary>
    /// <param name="input">The decision and its stored input.</param>
    /// <param name="at">When it was decided.</param>
    /// <param name="session">The session its details kept, if any.</param>
    public void SeedDecision(DecidedInput input, DateTimeOffset at, SessionId? session = null) => _decided.Add((input, at, session));

    /// <inheritdoc />
    public ValueTask<Result<bool>> AddItem(EvalItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Unavailable)
        {
            return Fail<bool>(_unavailable);
        }

        if (_items.Exists(i => string.Equals(i.Source, item.Source, StringComparison.Ordinal) || i.Id.Equals(item.Id)))
        {
            return Ok(value: false);
        }

        var parsed = InputJson.TryParse(item.InputJson, out var node) ? node : System.Text.Json.Nodes.JsonValue.Create(item.InputJson);
        _items.Add(item with { InputJson = InputCap.Write(_redactor.Redact(parsed), InputCap.MaxChars, TestJson.Options) });
        return Ok(value: true);
    }

    /// <inheritdoc />
    public ValueTask<Result<EvalItem>> FindItem(EvalItemId id, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<EvalItem>(_unavailable);
        }

        return Wrap(Find(id) is { } item ? Results.Success(item) : Results.Failure<EvalItem>(Unknown(id)));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> RecordLabel(EvalItemId item, LabelSource source, string label, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<Unit>(_unavailable);
        }

        if (Find(item) is null)
        {
            return Fail<Unit>(Unknown(item));
        }

        _labels[(item.Value, source)] = label;
        return Wrap(Results.Ok());
    }

    /// <inheritdoc />
    public ValueTask<Result<EvalItem?>> NextUnlabeled(string? question, LabelSource? source, IReadOnlySet<EvalItemId> exclude, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exclude);
        if (Unavailable)
        {
            return Fail<EvalItem?>(_unavailable);
        }

        var next = Ordered(_items).FirstOrDefault(i =>
            (question is null || string.Equals(i.Question, question, StringComparison.Ordinal))
            && !exclude.Contains(i.Id)
            && !_labels.Keys.Any(k => string.Equals(k.Item, i.Id.Value, StringComparison.Ordinal) && (source is null || k.Source == source)));
        return Wrap(Results.Success(next));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LabeledItem>>> Labeled(string question, LabelSource? source, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<IReadOnlyList<LabeledItem>>(_unavailable);
        }

        var labeled = new List<LabeledItem>();
        foreach (var item in Ordered(_items.Where(i => string.Equals(i.Question, question, StringComparison.Ordinal))))
        {
            var strongest = _strongestFirst
                .Where(s => (source is null || s == source) && _labels.TryGetValue((item.Id.Value, s), out var label) && !IsSkip(label))
                .Cast<LabelSource?>()
                .FirstOrDefault();
            if (strongest is { } from)
            {
                labeled.Add(new LabeledItem(item, _labels[(item.Id.Value, from)], from));
            }
        }

        return Ok<IReadOnlyList<LabeledItem>>(labeled);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LabelCount>>> Counts(CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<IReadOnlyList<LabelCount>>(_unavailable);
        }

        IReadOnlyList<LabelCount> counts = [.. _items.GroupBy(static i => i.Question, StringComparer.Ordinal).OrderBy(static g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var labeled = g.Count(i => Labels(i).Any(l => !IsSkip(l)));
            var skipped = g.Count(i => Labels(i).Any(IsSkip) && Labels(i).All(IsSkip));
            return new LabelCount(g.Key, g.Count(), labeled, skipped);
        })];
        return Wrap(Results.Success(counts));
    }

    /// <inheritdoc />
    public ValueTask<Result<LabelAgreement>> Agreement(string question, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<LabelAgreement>(_unavailable);
        }

        var shared = _items
            .Where(i => string.Equals(i.Question, question, StringComparison.Ordinal))
            .Select(i => (Outcome: Label(i, LabelSource.Outcome), Adjudicator: Label(i, LabelSource.Adjudicator)))
            .Where(p => p.Outcome is not null && p.Adjudicator is not null && !IsSkip(p.Outcome) && !IsSkip(p.Adjudicator))
            .ToList();
        return Wrap(Results.Success(new LabelAgreement(shared.Count, shared.Count(p => string.Equals(p.Outcome, p.Adjudicator, StringComparison.Ordinal)))));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<DecidedInput>>> DecidedSince(string? question, DateTimeOffset since, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<IReadOnlyList<DecidedInput>>(_unavailable);
        }

        return Ok<IReadOnlyList<DecidedInput>>([.. _decided
            .Where(d => d.At >= since && (question is null || string.Equals(d.Input.Question, question, StringComparison.Ordinal)))
            .OrderBy(static d => d.At).ThenBy(static d => d.Input.Id.Value, StringComparer.Ordinal)
            .Select(static d => d.Input)]);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<OutcomeCandidate>>> OutcomeCandidates(CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Fail<IReadOnlyList<OutcomeCandidate>>(_unavailable);
        }

        return Ok<IReadOnlyList<OutcomeCandidate>>([.. _items
            .Where(i => string.Equals(i.Question, "ask-check", StringComparison.Ordinal) && Label(i, LabelSource.Outcome) is null)
            .SelectMany(i => _decided.Where(d => string.Equals("decision:" + d.Input.Id.Value, i.Source, StringComparison.Ordinal)).Select(d => (Item: i, Decided: d)))
            .OrderBy(static p => p.Decided.At).ThenBy(static p => p.Item.Id.Value, StringComparer.Ordinal)
            .Select(static p => new OutcomeCandidate(p.Item.Id, p.Decided.At, p.Decided.Input.Answer ?? "", p.Decided.Session))]);
    }

    private static bool IsSkip(string? label) => string.Equals(label, LabelQuestionsSkip, StringComparison.Ordinal);

    private const string LabelQuestionsSkip = "skip";

    private static Error Unknown(EvalItemId id) => JudgeErrors.UnknownItem.With($"unknown item: {id.Value}");

    private static IEnumerable<EvalItem> Ordered(IEnumerable<EvalItem> items) =>
        items.OrderBy(static i => i.CreatedAt).ThenBy(static i => i.Id.Value, StringComparer.Ordinal);

    private static ValueTask<Result<T>> Fail<T>(Error error) => ValueTask.FromResult(Results.Failure<T>(error));

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult(Results.Success(value));

    private static ValueTask<Result<T>> Wrap<T>(Result<T> result) => ValueTask.FromResult(result);

    private EvalItem? Find(EvalItemId id) => _items.Find(i => i.Id.Equals(id));

    private string? Label(EvalItem item, LabelSource source) => _labels.GetValueOrDefault((item.Id.Value, source));

    private IEnumerable<string> Labels(EvalItem item) =>
        _labels.Where(l => string.Equals(l.Key.Item, item.Id.Value, StringComparison.Ordinal)).Select(static l => l.Value);
}
