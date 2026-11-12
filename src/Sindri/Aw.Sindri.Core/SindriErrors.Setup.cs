using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-SETUP-008: the scan root is missing, unreadable or outside the home folder (first-run spec §3.3, §6).</summary>
    public static readonly ErrorDefinition SetupScanRootInvalid = new("SND-SETUP-008", ErrorKind.Invalid, "Couldn't read that folder (fix: sindri setup scan --root DIR)");

    /// <summary>SND-SETUP-009: restart refused because agent sessions, workflow runs or jobs are open (first-run spec decision 11).</summary>
    public static readonly ErrorDefinition SetupRestartRunsOpen = new("SND-SETUP-009", ErrorKind.Conflict, "Sindri restarts when the open runs finish (fix: sindri restart --wait; sindri wf run-show <run> for each)");

    /// <summary>SND-SETUP-010: `sindri setup --apply` with no repo named and none known.</summary>
    public static readonly ErrorDefinition SetupNoRepoToAdd = new("SND-SETUP-010", ErrorKind.Invalid, "Name a repo to add: no repo here is one you worked on in GitHub lately (fix: sindri setup --apply --repo <path>)");

    /// <summary>SND-SETUP-011: restart with an installed build that has no build-info.</summary>
    public static readonly ErrorDefinition SetupInstallUnfinished = new("SND-SETUP-011", ErrorKind.Invalid, "The installed Sindri isn't finished installing (fix: scripts/install-cs.sh --link)");

    /// <summary>SND-SETUP-012: serve didn't answer 30 seconds after a start or restart.</summary>
    public static readonly ErrorDefinition SetupServeDidNotStart = new("SND-SETUP-012", ErrorKind.Unavailable, "Sindri didn't start (fix: sindri serve)");

    /// <summary>SND-SETUP-013: GitHub or Linear refused Sindri's OAuth app, an org policy.</summary>
    public static readonly ErrorDefinition SetupOauthBlocked = new("SND-SETUP-013", ErrorKind.Forbidden, "Your org blocks Sindri's app. Paste a token instead (fix: sindri setup connect <provider> --token-env NAME)");

    /// <summary>SND-SETUP-014: this build ships no client id for the provider and the profile sets none (`setup.oauth.github` or `setup.oauth.linear`).</summary>
    public static readonly ErrorDefinition SetupNoClientId = new("SND-SETUP-014", ErrorKind.Conflict, "This build has no app id for that provider. Paste a token instead (fix: sindri setup connect <provider> --token-env NAME)");

    /// <summary>SND-SETUP-015: `sindri setup --apply`'s setup pass didn't finish within --timeout.</summary>
    public static readonly ErrorDefinition SetupPassTimedOut = new("SND-SETUP-015", ErrorKind.Unavailable, "The setup pass didn't finish in time; it keeps running (fix: sindri jobs show <id>, then sindri setup)");

    /// <summary>SND-SETUP-016: `sindri setup --apply`'s setup pass failed.</summary>
    public static readonly ErrorDefinition SetupPassFailed = new("SND-SETUP-016", ErrorKind.Unavailable, "The setup pass failed (fix: sindri jobs show <id>)");
}
