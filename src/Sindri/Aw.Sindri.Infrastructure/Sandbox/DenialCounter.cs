using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// One launch's denial counter, 'denials/{launch id}.json' in the launch directory (sandbox spec §3.6): a count per (kind, target, rule), at most 200 distinct
/// (kind, target); the rest are counted per kind in the overflow rows. Targets are scrubbed and cut to 200 characters before they are
/// stored or sent, and are never a whole command line. The 1st, 10th and 100th count of each entry, and the first overflow of each kind,
/// queue a 'sandbox.denied' event that <see cref="Publish"/> sends to <see cref="Sink"/>.
/// </summary>
/// <param name="launch">The launch.</param>
/// <param name="path">The file.</param>
/// <param name="json">The package's AwJson options.</param>
/// <param name="scrubber">Takes secrets out of a target.</param>
/// <param name="clock">The time.</param>
internal sealed class DenialCounter(LaunchId launch, string path, JsonSerializerOptions json, ITextScrubber scrubber, TimeProvider clock)
{
    /// <summary>How many distinct (kind, target) pairs are counted one by one.</summary>
    internal const int MaxTargets = 200;

    internal const int MaxTargetLength = 200;

    private readonly Lock _lock = new();
    private readonly Dictionary<(DenialKind Kind, string Target, string Rule), Row> _rows = [];
    private readonly HashSet<(DenialKind Kind, string Target)> _targets = [];
    private readonly Dictionary<DenialKind, int> _overflow = [];
    private readonly List<SandboxDenied> _pending = [];
    private int _version;
    private int _written = -1;

    /// <summary>Where the events go; null drops them (the file is still written).</summary>
    public ISandboxDenials? Sink { get; init; }

    /// <summary>Counts one denial. Safe to call from any thread.</summary>
    /// <param name="observation">What was denied.</param>
    public void Record(DenialObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var target = Clean(observation.Target);
        var now = clock.GetUtcNow();
        lock (_lock)
        {
            _version++;
            var key = (observation.Kind, target, observation.Rule);
            if (_rows.TryGetValue(key, out var row))
            {
                row.Count++;
                row.Last = now;
                Queue(observation.Kind, target, observation.Rule, row.Count);
                return;
            }

            if (_targets.Count >= MaxTargets && !_targets.Contains((observation.Kind, target)))
            {
                var seen = _overflow.GetValueOrDefault(observation.Kind) + 1;
                _overflow[observation.Kind] = seen;
                if (seen == 1)
                {
                    _pending.Add(new SandboxDenied(launch, observation.Kind, string.Empty, DenialRules.Overflow, 1, Overflow: true));
                }

                return;
            }

            _ = _targets.Add((observation.Kind, target));
            _rows[key] = new Row(now);
            Queue(observation.Kind, target, observation.Rule, 1);
        }
    }

    /// <summary>The count for one entry.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="target">The target as it was given (it is cleaned the same way).</param>
    /// <param name="rule">The rule id.</param>
    /// <returns>The count, 0 when none.</returns>
    public int Count(DenialKind kind, string target, string rule)
    {
        var clean = Clean(target);
        lock (_lock)
        {
            return _rows.TryGetValue((kind, clean, rule), out var row) ? row.Count : 0;
        }
    }

    /// <summary>The file as it would be written now.</summary>
    /// <returns>The entries sorted by kind, target and rule, and the overflow rows.</returns>
    public DenialFile Snapshot()
    {
        lock (_lock)
        {
            return Build();
        }
    }

    /// <summary>Writes the file (temp file and rename) when something changed since the last write. Safe to call from any thread.</summary>
    public void Flush()
    {
        lock (_lock)
        {
            if (_version == _written)
            {
                return;
            }

            OwnerOnly.EnsureDirectory(Path.GetDirectoryName(path)!);   // why: the 'denials' folder is created by the first flush, not by the launcher
            OwnerOnly.WriteAtomically(path, AwJson.Serialize(Build(), json));
            _written = _version;
        }
    }

    /// <summary>Sends the queued events to <see cref="Sink"/>; one the ledger refused stays queued for the next call (the keys make a resend safe).</summary>
    /// <param name="cancellationToken">Cancels the sends.</param>
    /// <returns>A task that ends when the queue is empty or a send was refused.</returns>
    public async ValueTask Publish(CancellationToken cancellationToken)
    {
        var sink = Sink;
        SandboxDenied[] batch;
        lock (_lock)
        {
            batch = [.. _pending];
            _pending.Clear();
        }

        if (sink is null)
        {
            return;
        }

        for (var i = 0; i < batch.Length; i++)
        {
            Result<Unit> sent;
            try
            {
                sent = await sink.Append(batch[i], cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Requeue(batch[i..]);
                throw;
            }

            if (sent.IsError)
            {
                Requeue(batch[i..]);
                return;
            }
        }
    }

    private void Requeue(SandboxDenied[] unsent)
    {
        lock (_lock)
        {
            _pending.InsertRange(0, unsent);
        }
    }

    private void Queue(DenialKind kind, string target, string rule, int count)
    {
        if (count is 1 or 10 or 100)
        {
            _pending.Add(new SandboxDenied(launch, kind, target, rule, count));
        }
    }

    private DenialFile Build() => new(
        launch,
        clock.GetUtcNow(),
        [.. _rows.OrderBy(r => r.Key.Kind).ThenBy(r => r.Key.Target, StringComparer.Ordinal).ThenBy(r => r.Key.Rule, StringComparer.Ordinal)
            .Select(r => new DenialEntry(r.Key.Kind, r.Key.Target, r.Key.Rule, r.Value.Count, r.Value.First, r.Value.Last))],
        [.. _overflow.OrderBy(o => o.Key).Select(o => new DenialOverflow(o.Key, o.Value))]);

    // Scrub first, then cut: a cut can split a token so the scrubber no longer knows it.
    private string Clean(string target)
    {
        var scrubbed = scrubber.Scrub(target ?? string.Empty);
        var text = new StringBuilder(Math.Min(scrubbed.Length, MaxTargetLength));
        foreach (var c in scrubbed.Take(MaxTargetLength))
        {
            _ = text.Append(char.IsControl(c) ? '_' : c);
        }

        return text.ToString();
    }

    private sealed class Row(DateTimeOffset at)
    {
        public int Count { get; set; } = 1;

        public DateTimeOffset First { get; } = at;

        public DateTimeOffset Last { get; set; } = at;
    }
}
