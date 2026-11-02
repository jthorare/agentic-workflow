using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An in-memory <see cref="ILabelCache"/>; <see cref="Failure"/> makes every call fail.</summary>
public sealed class FakeLabelCache : ILabelCache
{
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);

    /// <summary>When set, every call fails with it.</summary>
    public Error? Failure { get; set; }

    /// <summary>The keys of every Find, in call order.</summary>
    public IList<IReadOnlyList<string>> Asked { get; } = [];

    /// <summary>The labels saved so far, by key.</summary>
    public IReadOnlyDictionary<string, string> Saved => _labels;

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Find(IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<IReadOnlyDictionary<string, string>>>(failure);
        }

        Asked.Add(keys);
        IReadOnlyDictionary<string, string> found = keys.Where(_labels.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(k => k, k => _labels[k], StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(found));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Save(IReadOnlyList<KeyValuePair<string, string>> labels, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        foreach (var (key, label) in labels)
        {
            _labels[key] = label;
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
