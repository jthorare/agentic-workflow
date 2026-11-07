using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>The Linear hub's errors that Context itself raises (spec 2026-11-04 §8): the write-through's and the write path's. The rest of the SND-LINEAR codes (001 to 003, 006, 007, 409, 014) belong to Sindri's write commands. SND-LINEAR-002 is Sindri's too (a write with no write key): a text over a limit is 015, as if Linear had refused it.</summary>
public static class LinearHubErrors
{
    /// <summary>SND-LINEAR-013: Linear applied the change, but the answer was not stored (a purge of the linear source was running, or the re-read or the commit failed). Nothing is lost: the next sync stores it.</summary>
    public static readonly ErrorDefinition NotStored = new("SND-LINEAR-013", ErrorKind.Unavailable, "Saved in Linear; Sindri shows it after the next sync");

    /// <summary>SND-LINEAR-004: Linear refused the write key (revoked, or read-only).</summary>
    public static readonly ErrorDefinition WriteKeyRefused = new("SND-LINEAR-004", ErrorKind.Unavailable, "Linear refused the write key. Make a new one with write permission");

    /// <summary>SND-LINEAR-005: no such issue in the profile's teams (deleted, moved, or not synced yet), by a live read or a write.</summary>
    public static readonly ErrorDefinition IssueNotFound = new("SND-LINEAR-005", ErrorKind.NotFound, "No such issue in your teams");

    /// <summary>SND-LINEAR-010: Linear couldn't be reached before anything was sent, so nothing was changed.</summary>
    public static readonly ErrorDefinition Unreachable = new("SND-LINEAR-010", ErrorKind.Unavailable, "Couldn't reach Linear, so nothing was changed");

    /// <summary>SND-LINEAR-011: the write was sent and no answer came. A write that gets this error instead of <see cref="LinearWriteUnknown"/> never reached the re-read.</summary>
    public static readonly ErrorDefinition NoAnswer = new("SND-LINEAR-011", ErrorKind.Timeout, "Linear didn't confirm it");

    /// <summary>SND-LINEAR-012: Linear's rate limit; nothing was sent. The message names the time to try again.</summary>
    public static readonly ErrorDefinition RateLimited = new("SND-LINEAR-012", ErrorKind.RateLimited, "Linear's rate limit");

    /// <summary>SND-LINEAR-015: Linear refused the write with a validation error, or the text was over a limit Context checks first (a title of 1 to 255 characters, a description or comment of up to 65,536). Nothing was changed.</summary>
    public static readonly ErrorDefinition Rejected = new("SND-LINEAR-015", ErrorKind.Invalid, "Linear said no");
}
