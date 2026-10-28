using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Cursor CLI's 'permissions' rules, as far as a Step's policy uses them: Shell(...), Read(...), Write(...), and deny wins.</summary>
/// <param name="Allow">Rules allowed without asking.</param>
internal sealed record CursorPermissions(IReadOnlyList<string> Allow)
{
    /// <summary>Rules that are always refused (the Step's denied paths, a read-only path's writes), or absent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Deny { get; init; }
}
