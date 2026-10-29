namespace Aw.Context.Core.Eval.Backtest;

/// <summary>An issue a sample could come from: refs and versions only, never text (spec §9.1).</summary>
/// <param name="Key">The tracker key, 'SPL-103'.</param>
/// <param name="RecordRef">The record's reference in its source ('linear/…').</param>
/// <param name="Version">The record's version when listed.</param>
/// <param name="CreatedAt">When the issue was created: the sample's cut.</param>
/// <param name="UpdatedAt">When it was last edited; after the cut the brief is leaky.</param>
/// <param name="Title">The issue title as the source gave it. Held in memory for the run; a report never carries it.</param>
public sealed record BriefIssue(string Key, string RecordRef, string? Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Title = "");
