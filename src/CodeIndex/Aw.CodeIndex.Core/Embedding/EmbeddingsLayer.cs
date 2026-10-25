using System.Globalization;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Embedding;

/// <summary>
/// The embeddings layer (spec §5.6, §5.7). A build only <see cref="Record(IIndexBuild, EmbeddingsSettings, CancellationToken)"/>s where the layer stands from the counts; the embed slice
/// runs <see cref="Run"/> outside the heavy slot: it probes the model, embeds the symbols with stored text in batches and records the row.
/// </summary>
/// <param name="embedder">The embedding model.</param>
/// <param name="clock">The time source.</param>
public sealed class EmbeddingsLayer(IEmbedder embedder, TimeProvider clock)
{
    /// <summary>Records the layer's row from the counts, without calling the model: ok when every symbol has a vector, pending while some have not (the embed slice finishes them), disabled when the profile turns embeddings off.</summary>
    /// <param name="build">The build file.</param>
    /// <param name="settings">The embeddings settings.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many symbols have a vector and how many should; an error only when the build file cannot be read or written.</returns>
    public async ValueTask<Result<EmbeddingProgress>> Record(IIndexBuild build, EmbeddingsSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Enabled
            ? await Settle(build, embedder.ModelFor(settings), failure: null, cancellationToken).ConfigureAwait(false)
            : await Disabled(build, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Embeds up to <paramref name="limit"/> pending symbols and records the layer's row; a model that fails leaves the layer unavailable with the failure's code, never a failed build.</summary>
    /// <param name="build">The build file.</param>
    /// <param name="settings">The embeddings settings.</param>
    /// <param name="progress">Told a counts-only line per batch.</param>
    /// <param name="limit">The most symbols to embed in this call.</param>
    /// <param name="cancellationToken">Cancels the layer between batches.</param>
    /// <returns>How many symbols have a vector and how many should; an error only when the build file cannot be read or written.</returns>
    public async ValueTask<Result<EmbeddingProgress>> Run(IIndexBuild build, EmbeddingsSettings settings, IProgress<string> progress, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progress);
        if (!settings.Enabled)
        {
            return await Disabled(build, cancellationToken).ConfigureAwait(false);
        }

        var model = embedder.ModelFor(settings);
        var probed = await embedder.Probe(settings, cancellationToken).ConfigureAwait(false);
        if (probed.IsError)
        {
            // nothing is made, so every vector now there was carried
            var failed = await Settle(build, model, probed.Error, cancellationToken).ConfigureAwait(false);
            return failed.IsError ? failed : failed.Value with { Carried = failed.Value.Embedded };
        }

        return await Embed(build, new EmbedPass(settings, model, progress, limit), cancellationToken).ConfigureAwait(false);
    }

    private sealed record EmbedPass(EmbeddingsSettings Settings, string Model, IProgress<string> Progress, int Limit);

    private async ValueTask<Result<EmbeddingProgress>> Embed(IIndexBuild build, EmbedPass pass, CancellationToken cancellationToken)
    {
        var (settings, model, progress) = (pass.Settings, pass.Model, pass.Progress);
        var before = await build.CountEmbeddings(cancellationToken).ConfigureAwait(false);
        var candidates = await build.EmbedCandidates(pass.Limit, cancellationToken).ConfigureAwait(false);
        if (before.IsError || candidates.IsError)
        {
            return before.IsError ? before.Error : candidates.Error;
        }

        var done = before.Value.Embedded;
        var made = 0;
        var carried = before.Value.Embedded;
        foreach (var batch in candidates.Value.Chunk(CodeIndexRules.EmbedBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vectors = await embedder.Embed(settings, [.. batch.Select(EmbedText.Of)], cancellationToken).ConfigureAwait(false);
            if (vectors.IsError)
            {
                return Tally(await Settle(build, model, vectors.Error, cancellationToken).ConfigureAwait(false), carried, made);
            }

            var written = await build.WriteEmbeddings([.. batch.Select((c, i) => new EmbeddingRecord(c.Symbol, model, vectors.Value[i]))], cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }

            done += batch.Length;
            made += batch.Length;
            progress.Report(string.Create(CultureInfo.InvariantCulture, $"embeddings {done:N0} of {before.Value.Embeddable:N0}"));
        }

        return Tally(await Settle(build, model, failure: null, cancellationToken).ConfigureAwait(false), carried, made);
    }

    // Records what the layer ended as: unavailable with the failure's code when the model failed (the vectors made so far stay),
    // otherwise ok when every symbol has a vector and pending when the cap left some for the next build.
    private async ValueTask<Result<EmbeddingProgress>> Settle(IIndexBuild build, string model, Error? failure, CancellationToken cancellationToken)
    {
        var counted = await build.CountEmbeddings(cancellationToken).ConfigureAwait(false);
        if (counted.IsError)
        {
            return counted.Error;
        }

        var (embedded, embeddable) = (counted.Value.Embedded, counted.Value.Embeddable);
        var state = failure is not null ? LayerState.Unavailable : embedded >= embeddable ? LayerState.Ok : LayerState.Pending;
        var detail = string.Create(CultureInfo.InvariantCulture, $"{embedded} of {embeddable} embedded");
        var recorded = await Record(build, new LayerRecord(LayerName.Embeddings, model, state, failure?.Code, detail, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : counted.Value;
    }

    // A run's result also says how many vectors it found already there (carried) and how many it made.
    private static Result<EmbeddingProgress> Tally(Result<EmbeddingProgress> settled, int carried, int made)
        => settled.IsError ? settled : settled.Value with { Carried = carried, Made = made };

    private async ValueTask<Result<EmbeddingProgress>> Disabled(IIndexBuild build, CancellationToken cancellationToken)
    {
        var off = await Record(build, new LayerRecord(LayerName.Embeddings, "none", LayerState.Disabled, Code: null, "disabled by the profile", clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return off.IsError ? off.Error : new EmbeddingProgress(0, 0);
    }

    private static async ValueTask<Result<Unit>> Record(IIndexBuild build, LayerRecord layer, CancellationToken cancellationToken)
        => await build.SetLayer(layer, cancellationToken).ConfigureAwait(false);
}
