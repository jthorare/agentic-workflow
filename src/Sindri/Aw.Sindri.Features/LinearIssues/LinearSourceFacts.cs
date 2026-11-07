namespace Aw.Sindri.Features.LinearIssues;

/// <summary>What the stored Linear source says before any issue is read.</summary>
/// <param name="Connected">Whether the profile has a Linear source.</param>
/// <param name="Viewer">Who the source's key belongs to, or null while the viewer step hasn't run.</param>
/// <param name="Feed">The issue feed's health.</param>
internal sealed record LinearSourceFacts(bool Connected, LinearPersonView? Viewer, LinearFeedView Feed);
