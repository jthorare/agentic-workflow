namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>One open PR of a project.</summary>
/// <param name="Url">The PR's link on the forge, or "https://github.com/owner/name/pull/N" built from the record when the record has none.</param>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Title">The title; text from the forge, untrusted.</param>
/// <param name="State">What it waits on.</param>
/// <param name="DecidedAt">For ciRed, when Reviews decided the CI roll-up of the failing head (not when CI went red); else null.</param>
/// <param name="WaitingOn">The reviewers it waits on, comma separated; null unless waiting.</param>
public sealed record ProjectTruthPr(string Url, string Repo, int Number, string Title, ProjectPrState State, DateTimeOffset? DecidedAt, string? WaitingOn);
