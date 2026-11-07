namespace Aw.Sindri.Core.Reviews;

/// <summary>One required check that applies to a PR, decided (PR review page spec §7.3).</summary>
/// <param name="Key">The check's id in the settings.</param>
/// <param name="Title">The check's title.</param>
/// <param name="Need">The kind of need: ci, changedWith, covers, body or manual.</param>
/// <param name="Status">How it stands.</param>
/// <param name="Detail">A short reason ("ui-tests: failure on abc1234"), or null.</param>
public sealed record RequiredCheckResult(string Key, string Title, string Need, RequiredCheckStatus Status, string? Detail)
{
    /// <summary>Whether the check is still open: everything that is not a pass or a tick.</summary>
    public bool IsOpen => Status is not (RequiredCheckStatus.Pass or RequiredCheckStatus.Ticked);
}
