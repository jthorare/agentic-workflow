namespace Aw.Sindri.Core.Profile;

/// <summary>The pure half of 'repo status': the one-line nudge and the plain text.</summary>
public static class RepoStatusRules
{
    /// <summary>The SessionStart nudge: one line when the repo is not onboarded, else empty. Silent before any approval, so a user who never set up a profile isn't nagged in every repo.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The line, or an empty string.</returns>
    public static string NudgeLine(RepoStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.State != RepoState.NotOnboarded)
        {
            return string.Empty;
        }

        return status.Name is { } name
            ? $"sindri: {name} is not onboarded yet; run: sindri repo onboard"
            : $"sindri: {BaseName(status.Path)} is not onboarded (no secret scan or index); run: sindri repo onboard";
    }

    private static string BaseName(string? path)
    {
        var trimmed = (path ?? string.Empty).TrimEnd('/');
        return trimmed[(trimmed.LastIndexOf('/') + 1)..];
    }

    /// <summary>The plain 'repo status' line.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The text, no trailing newline.</returns>
    public static string Text(RepoStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return status.State switch
        {
            RepoState.Onboarded => $"{status.Name} is onboarded.",
            RepoState.OutsideGit => "Not inside a git repo.",
            RepoState.NotOnboarded => NudgeLine(status),
            RepoState.NoApprovedProfile => "No profile yet: sindri profile init, then sindri repo add <path>.",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, "Not a repo state"),
        };
    }

    /// <summary>The exit code of the plain verb: 0 onboarded, 1 anything else.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The code.</returns>
    public static int ExitCode(RepoStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return status.State == RepoState.Onboarded ? 0 : 1;
    }
}
