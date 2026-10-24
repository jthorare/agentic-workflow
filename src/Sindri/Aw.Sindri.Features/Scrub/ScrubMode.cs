namespace Aw.Sindri.Features.Scrub;

/// <summary>Which 'sindri scrub' form ran.</summary>
public enum ScrubMode
{
    /// <summary>'scrub': standard input in, the redacted text out.</summary>
    Stdin,

    /// <summary>'scrub --staged': the added lines of the staged diff are scanned; a finding is SND-SCRUB-002.</summary>
    Staged,

    /// <summary>'scrub --install-pre-commit [--repo PATH] [--bin PATH]': the pre-commit hook is written.</summary>
    InstallPreCommit,
}
