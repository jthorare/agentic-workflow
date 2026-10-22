using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary><see cref="IHoldoutStore"/> on disk: <c language="text">$AW_SINDRI_HOME/holdout/&lt;line&gt;/&lt;phase&gt;/</c>, directories 0700 and files 0600 (line spec §9.1).</summary>
/// <param name="settings">Sindri's home.</param>
internal sealed class DiskHoldoutStore(SindriSettings settings) : IHoldoutStore
{
    /// <inheritdoc/>
    public async ValueTask<Result<HoldoutSet>> Set(string line, string phase, CancellationToken cancellationToken)
    {
        var read = await ReadFile(line, phase, HoldoutFiles.SetName, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : HoldoutFiles.ParseSet(read.Value);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<HoldoutSeal>> Seal(string line, string phase, CancellationToken cancellationToken)
    {
        var read = await ReadFile(line, phase, HoldoutFiles.SealName, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : HoldoutFiles.ParseSeal(read.Value);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlySet<string>>> Spent(string line, string phase, CancellationToken cancellationToken)
    {
        var path = PathOf(line, phase, HoldoutFiles.SpentName);
        if (path.IsError)
        {
            return path.Error;
        }

        try
        {
            return HoldoutFiles.ParseSpent(File.Exists(path.Value) ? await File.ReadAllTextAsync(path.Value, cancellationToken).ConfigureAwait(false) : string.Empty);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return HoldoutFiles.Unreadable(HoldoutFiles.SpentName, e.Message);   // boundary: disk
        }
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

        var spent = await Spent(line, phase, cancellationToken).ConfigureAwait(false);
        if (spent.IsError)
        {
            return spent.Error;
        }

        return Write(line, phase, HoldoutFiles.SpentJson(spent.Value.Concat(scenarios).Distinct(StringComparer.Ordinal)));
    }

    /// <summary>The phase's directory.</summary>
    /// <param name="line">The line's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <returns>The path.</returns>
    internal string Directory(string line, string phase) => Path.Combine(settings.Home, "holdout", line, phase);

    private Result<bool> Write(string line, string phase, string text)
    {
        try
        {
            var dir = Directory(line, phase);
            OwnerOnly.EnsureDirectory(Path.Combine(settings.Home, "holdout"));
            OwnerOnly.EnsureDirectory(Path.Combine(settings.Home, "holdout", line));
            OwnerOnly.EnsureDirectory(dir);
            OwnerOnly.WriteAtomically(Path.Combine(dir, HoldoutFiles.SpentName), System.Text.Encoding.UTF8.GetBytes(text));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return HoldoutFiles.Unreadable(HoldoutFiles.SpentName, $"it can't be written: {e.Message}");   // boundary: disk
        }
    }

    private async ValueTask<Result<string>> ReadFile(string line, string phase, string name, CancellationToken cancellationToken)
    {
        var path = PathOf(line, phase, name);
        if (path.IsError)
        {
            return path.Error;
        }

        try
        {
            return File.Exists(path.Value)
                ? await File.ReadAllTextAsync(path.Value, cancellationToken).ConfigureAwait(false)
                : WorkflowErrors.HoldoutSetMissing.With($"the phase '{phase}' of line '{line}' has no {name} in the holdout store (fix: run step.plan.holdout for the phase)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return HoldoutFiles.Unreadable(name, e.Message);   // boundary: disk
        }
    }

    private Result<string> PathOf(string line, string phase, string name) =>
        HoldoutFiles.IsKey(line) && HoldoutFiles.IsKey(phase)
            ? Path.Combine(Directory(line, phase), name)
            : SindriErrors.InvalidInput.With("a holdout line and phase are each one path segment of letters, digits, '.', '_' and '-', not starting with '.'");
}
