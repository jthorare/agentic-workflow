using Aw.Context.Core.Extract;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An <see cref="ILabeler"/> that names every cluster 'Labeled &lt;id&gt;' (or fails with <see cref="Failure"/>) and records the clusters it was shown.</summary>
public sealed class FakeLabeler : ILabeler
{
    /// <summary>When set, every pass fails with it.</summary>
    public Error? Failure { get; set; }

    /// <summary>When true, the pass names every cluster but reports it incomplete (a later batch failed).</summary>
    public bool Incomplete { get; set; }

    /// <summary>When set, every pass succeeds with no labels and this as its <see cref="LabelPass.Unavailable"/> note.</summary>
    public string? Unavailable { get; set; }

    /// <summary>The tokens every pass reports it used.</summary>
    public int Spent { get; set; }

    /// <summary>The clusters shown to the model, across passes.</summary>
    public IList<FileCluster> Shown { get; } = [];

    /// <summary>The extra scrub patterns each pass was given.</summary>
    public IList<IReadOnlyList<ScrubPattern>> Patterns { get; } = [];

    /// <summary>The options each pass was given (null when the caller gave none).</summary>
    public IList<LabelOptions?> Options { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<LabelPass>> Label(IReadOnlyList<FileCluster> clusters, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, LabelOptions? options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<LabelPass>>(failure);
        }

        if (Unavailable is { } note)
        {
            return ValueTask.FromResult(Results.Success(new LabelPass([], Complete: false, Unavailable: note)));
        }

        Patterns.Add(extraPatterns);
        Options.Add(options);
        foreach (var cluster in clusters)
        {
            Shown.Add(cluster);
        }

        return ValueTask.FromResult(Results.Success(new LabelPass([.. clusters.Select(c => new ClusterLabel(c.Id, "Labeled " + c.Id.Value))], Complete: !Incomplete) { Spent = Spent }));
    }
}
