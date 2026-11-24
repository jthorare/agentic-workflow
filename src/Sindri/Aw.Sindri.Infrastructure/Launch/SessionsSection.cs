using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'sessions' section (collector spec §9).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SessionsSection
{
    public bool? Enabled { get; init; }

    public int? PollSeconds { get; init; }

    public IReadOnlyList<string>? Providers { get; init; }

    public IReadOnlyList<string>? TmuxSockets { get; init; }

    public string? Scope { get; init; }

    public string? ClaudeSessionsDir { get; init; }
}
