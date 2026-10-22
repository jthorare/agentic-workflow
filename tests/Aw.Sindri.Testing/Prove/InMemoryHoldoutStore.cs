using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing.Prove;

/// <summary>The in-memory <see cref="IHoldoutStore"/>: files by line and phase, read through <see cref="HoldoutFiles"/> as the disk store does; it can be made to refuse every write.</summary>
public sealed class InMemoryHoldoutStore : IHoldoutStore
{
    private readonly Dictionary<(string Line, string Phase), (string? Set, string? Seal)> _files = [];
    private readonly Dictionary<(string Line, string Phase), HashSet<string>> _spent = [];

    /// <summary>Whether every write fails, as an unwritable store does.</summary>
    public bool Unwritable { get; set; }

    /// <summary>Puts a phase's files in the store; a null text leaves that file out.</summary>
    /// <param name="line">The line's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="setText">The set file's text.</param>
    /// <param name="sealText">The seal file's text.</param>
    public void Put(string line, string phase, string? setText, string? sealText = null) => _files[(line, phase)] = (setText, sealText);

    /// <inheritdoc/>
    public ValueTask<Result<HoldoutSet>> Set(string line, string phase, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = Text(line, phase, HoldoutFiles.SetName, static f => f.Set);
        return ValueTask.FromResult(text.IsError ? text.Error : HoldoutFiles.ParseSet(text.Value));
    }

    /// <inheritdoc/>
    public ValueTask<Result<HoldoutSeal>> Seal(string line, string phase, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = Text(line, phase, HoldoutFiles.SealName, static f => f.Seal);
        return ValueTask.FromResult(text.IsError ? text.Error : HoldoutFiles.ParseSeal(text.Value));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlySet<string>>> Spent(string line, string phase, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Check(line, phase) is { } refused
            ? refused
            : Results.Success<IReadOnlySet<string>>(_spent.TryGetValue((line, phase), out var marked) ? new HashSet<string>(marked, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<bool>> MarkSpent(string line, string phase, IReadOnlyCollection<string> scenarios, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        var set = await Set(line, phase, cancellationToken).ConfigureAwait(false);
        if (set.IsError)
        {
            return set.Error;
        }

        if (scenarios.FirstOrDefault(s => !set.Value.Scenarios.Any(h => string.Equals(h.Key, s, StringComparison.Ordinal))) is { } unknown)
        {
            return SindriErrors.InvalidInput.With($"'{unknown}' is not a scenario of the phase's hidden set");
        }

        if (Unwritable)
        {
            return HoldoutFiles.Unreadable(HoldoutFiles.SpentName, "it can't be written");
        }

        if (!_spent.TryGetValue((line, phase), out var marked))
        {
            _spent[(line, phase)] = marked = new HashSet<string>(StringComparer.Ordinal);
        }

        marked.UnionWith(scenarios);
        return true;
    }

    private static Error? Check(string line, string phase) =>
        HoldoutFiles.IsKey(line) && HoldoutFiles.IsKey(phase) ? null : SindriErrors.InvalidInput.With("a holdout line and phase are each one path segment");

    private Result<string> Text(string line, string phase, string name, Func<(string? Set, string? Seal), string?> pick) =>
        Check(line, phase) is { } refused ? refused
        : _files.TryGetValue((line, phase), out var files) && pick(files) is { } text ? text
        : WorkflowErrors.HoldoutSetMissing.With($"the phase '{phase}' of line '{line}' has no {name} in the holdout store");
}
