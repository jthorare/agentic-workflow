namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>Where a read of the stored issues continues: stored lists run newest first by Linear's update time, then by record key, so a read continues after one issue.</summary>
/// <param name="UpdatedAt">That issue's update time.</param>
/// <param name="Record">That issue's record key.</param>
internal sealed record LinearIssuePosition(DateTimeOffset UpdatedAt, string Record);
