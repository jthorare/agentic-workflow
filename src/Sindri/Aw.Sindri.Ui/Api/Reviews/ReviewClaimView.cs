namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One Covers: ref of the PR and whether it resolves.</summary>
internal sealed record ReviewClaimView(string Ref, string State, string? Error);
