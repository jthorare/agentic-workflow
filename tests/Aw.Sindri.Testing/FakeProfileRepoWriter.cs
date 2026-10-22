using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The profile's repos in memory: the first append creates the profile. Kept honest by <see cref="ProfileRepoWriterContract"/>.</summary>
public sealed class FakeProfileRepoWriter : IProfileRepoWriter
{
    private readonly List<RepoEntry> _repos = [];
    private bool _created;

    /// <summary>Gets the repos written, in order.</summary>
    public IReadOnlyList<RepoEntry> Repos => _repos;

    /// <summary>Gets the effects the profile was created with, or null before it was.</summary>
    public IReadOnlyList<string>? CreatedWith { get; private set; }

    /// <summary>Gets or sets an error Append returns instead of writing.</summary>
    public Error? Fails { get; set; }

    /// <summary>Gets or sets what runs right after a repo is written, before Append answers: a tick or another add landing in the window before the caller records its event.</summary>
    public Func<RepoEntry, Task>? AfterAppend { get; set; }

    /// <inheritdoc/>
    public async ValueTask<Result<RepoAppended>> Append(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(defaultEffects);
        if (Fails is { } error)
        {
            return error;
        }

        if (_repos.Exists(r => r.Name == repo.Name && r.Path == repo.Path))
        {
            return RepoAppended.AlreadyThere;
        }

        if (_repos.Exists(r => string.Equals(r.Name, repo.Name, StringComparison.OrdinalIgnoreCase) || r.Path == repo.Path))
        {
            return SindriErrors.SetupRepoExists.With("another repo has that name or folder (fix: pick another folder, or give a different name)");
        }

        _repos.Add(repo);
        var outcome = _created ? RepoAppended.Appended : RepoAppended.Created;
        if (!_created)
        {
            _created = true;
            CreatedWith = [.. defaultEffects];
        }

        if (AfterAppend is { } after)
        {
            await after(repo).ConfigureAwait(false);
        }

        return outcome;
    }
}
