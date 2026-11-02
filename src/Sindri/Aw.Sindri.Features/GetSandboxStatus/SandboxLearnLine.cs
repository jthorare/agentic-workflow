
namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>A repo whose network learn mode is open.</summary>
/// <param name="Repo">The repo's name as the profile keys it.</param>
/// <param name="EndsAt">When learn mode ends on its own.</param>
public sealed record SandboxLearnLine(string Repo, DateTimeOffset EndsAt);
