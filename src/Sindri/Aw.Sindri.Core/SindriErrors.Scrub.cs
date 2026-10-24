using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-SCRUB-002: 'scrub --staged' found likely secrets in the added lines (exit 1, which the pre-commit hook turns into a refused commit).</summary>
    public static readonly ErrorDefinition ScrubStagedSecrets = new("SND-SCRUB-002", ErrorKind.Invalid, "Staged changes contain likely secrets");

    /// <summary>SND-SCRUB-003: a pre-commit hook that is not Sindri's is already installed, or core.hooksPath points at a directory Sindri did not create.</summary>
    public static readonly ErrorDefinition ScrubForeignHook = new("SND-SCRUB-003", ErrorKind.Conflict, "A different pre-commit hook is already installed");

    /// <summary>SND-SCRUB-004: 'scrub --staged' or '--install-pre-commit' ran outside a git repo.</summary>
    public static readonly ErrorDefinition ScrubNotARepo = new("SND-SCRUB-004", ErrorKind.Invalid, "Not a git repository");

    /// <summary>SND-SCRUB-005: 'git diff --cached' failed, so the staged changes could not be scanned.</summary>
    public static readonly ErrorDefinition ScrubGitFailed = new("SND-SCRUB-005", ErrorKind.Unavailable, "git diff --cached failed");
}
