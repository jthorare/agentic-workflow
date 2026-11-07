using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-REVIEW-001: a bad 'review' key: a login, panel, glob or regex, a duplicate id, an unknown 'settings' or 'extends', a cycle, 'when: []', a 'need' with other than one key (PR review page spec §9).</summary>
    public static readonly ErrorDefinition ReviewInvalid = new("SND-REVIEW-001", ErrorKind.Invalid, "A 'review' profile key is invalid");

    /// <summary>SND-REVIEW-002: a 'review.projects[].project' that is not on the Projects page.</summary>
    public static readonly ErrorDefinition ReviewProjectUnknown = new("SND-REVIEW-002", ErrorKind.Invalid, "That project is not on the Projects page");

    /// <summary>SND-REVIEW-003: a 'command:' need, which waits for the sandbox.</summary>
    public static readonly ErrorDefinition ReviewCommandCheck = new("SND-REVIEW-003", ErrorKind.Invalid, "Command checks wait for the sandbox");

    /// <summary>SND-REVIEW-005: a 'review.projects[].repos' entry that 'context.sources.github.repos' does not sync.</summary>
    public static readonly ErrorDefinition ReviewRepoUnsynced = new("SND-REVIEW-005", ErrorKind.Invalid, "Sindri doesn't sync that repo");

    /// <summary>SND-REVIEW-503: offline, and the PR is not cached yet.</summary>
    public static readonly ErrorDefinition ReviewOfflineUncached = new("SND-REVIEW-503", ErrorKind.Unavailable, "Offline, and this PR isn't cached yet");
}
