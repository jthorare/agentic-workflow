namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>One read of the stored issues, newest first, with the names resolved and no link or PR yet.</summary>
/// <param name="Issues">The issues.</param>
/// <param name="More">Whether the store holds issues after the last one.</param>
internal sealed record LinearIssueFactsPage(IReadOnlyList<LinearIssueRowView> Issues, bool More);
