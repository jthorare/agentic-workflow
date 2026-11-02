namespace Aw.Sindri.Ui.Api.Sources;

/// <summary>One context source. The first six fields are what the server has always served; every later one is null from a server that predates it.</summary>
/// <param name="Source">The source's name ("github", "linear", "slack").</param>
/// <param name="Phase">The source's phase as served, or null when it has not started.</param>
/// <param name="Usable">True when its records can be read.</param>
/// <param name="Complete">True when its first read is complete.</param>
/// <param name="LastSync">The last attempt, failed or not.</param>
/// <param name="LastCode">The code of the last attempt's outcome (SND-CONTEXT-059: capped this tick), or null.</param>
/// <param name="Pull">'idle', 'running', 'backoff' or 'failed'; a word this app doesn't know is shown as served.</param>
/// <param name="Records">How many records the source holds.</param>
/// <param name="LastCommit">The end of the last successful tick, or null when nothing was ever committed.</param>
/// <param name="LastError">The SND- code of the last failure; null after a success.</param>
/// <param name="LastErrorText">One scrubbed line saying what failed.</param>
/// <param name="Failures">Failed attempts in a row.</param>
/// <param name="NextTry">When a source in backoff tries again.</param>
internal sealed record IndexSourceView(
    string Source,
    string? Phase,
    bool Usable,
    bool Complete,
    DateTimeOffset? LastSync,
    string? LastCode,
    string? Pull = null,
    int? Records = null,
    DateTimeOffset? LastCommit = null,
    string? LastError = null,
    string? LastErrorText = null,
    int? Failures = null,
    DateTimeOffset? NextTry = null);
