using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// <see cref="IPrepareCache"/> on disk: <c lang="text">&lt;root&gt;/&lt;key&gt;/node_modules</c> (root is the real home's <c lang="text">cache/prepare</c>). A copy is a reflink where the filesystem has one
/// (<c lang="text">cp --reflink=auto</c>, <c lang="text">cp -c</c> on macOS) and a plain copy where it does not. A store lands under a temporary name and moves into place, so a half-copied install is never a hit.
/// </summary>
/// <param name="root">The cache directory.</param>
/// <param name="runner">Runs <c lang="text">cp</c>.</param>
internal sealed class FilePrepareCache(string root, IToolRunner runner) : IPrepareCache
{
    private const string Modules = "node_modules";
    private static readonly TimeSpan _copyBudget = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public bool Has(string key) => Directory.Exists(Path.Combine(root, key, Modules));

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Restore(string key, string packageDirectory, CancellationToken cancellationToken)
    {
        var target = Path.Combine(packageDirectory, Modules);
        if (Directory.Exists(target))
        {
            return ValueTask.FromResult(Results.Success(Unit.Value));   // a re-launch's worktree keeps the packages the session added; never overwritten
        }

        _ = Directory.CreateDirectory(packageDirectory);
        return Copy(Path.Combine(root, key, Modules), target, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Store(string key, string packageDirectory, CancellationToken cancellationToken)
    {
        var final = Path.Combine(root, key);
        if (Directory.Exists(Path.Combine(final, Modules)))
        {
            return Unit.Value;   // another prepare filled it first
        }

        var staging = Path.Combine(root, key + ".tmp-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(staging);
        var copied = await Copy(Path.Combine(packageDirectory, Modules), Path.Combine(staging, Modules), cancellationToken).ConfigureAwait(false);
        try
        {
            if (copied.IsSuccess && !Directory.Exists(final))
            {
                Directory.Move(staging, final);
                return Unit.Value;
            }

            return copied;
        }
        catch (IOException)
        {
            return Unit.Value;   // lost the race to another prepare: its copy is as good
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private async ValueTask<Result<Unit>> Copy(string from, string to, CancellationToken cancellationToken)
    {
        string[][] attempts = OperatingSystem.IsMacOS() ? [["-cR", from, to], ["-R", from, to]] : [["-a", "--reflink=auto", from, to]];
        var last = "no copy attempted";
        foreach (var arguments in attempts)
        {
            if (Directory.Exists(to))
            {
                Directory.Delete(to, recursive: true);   // a failed clone attempt may leave a partial tree
            }

            var run = await Task.Run(() => runner.Run("cp", arguments, _copyBudget, options: null, cancellationToken), CancellationToken.None).ConfigureAwait(false);
            if (run.Outcome == ToolOutcome.Cancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (run.Outcome == ToolOutcome.Exited && run.ExitCode == 0)
            {
                return Unit.Value;
            }

            last = run.Outcome == ToolOutcome.Exited ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"cp exited {run.ExitCode}: {run.Error}") : $"cp {run.Outcome}";
        }

        return KernelErrors.Unavailable.With($"the copy from {from} to {to} failed: {last}");
    }
}
