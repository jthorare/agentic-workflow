using Aw.Forge.Core.Pulls;

namespace Aw.Forge.Infrastructure.World;

/// <summary>One pull request in the file/fake forge.</summary>
internal sealed class WorldPull
{
    public string Host { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public string Repo { get; set; } = string.Empty;

    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public PullStatus Status { get; set; } = PullStatus.Open;

    public bool Draft { get; set; }

    public List<string> Labels { get; set; } = [];

    public string BaseRef { get; set; } = string.Empty;

    public string BaseSha { get; set; } = string.Empty;

    public string HeadRef { get; set; } = string.Empty;

    public string HeadSha { get; set; } = string.Empty;

    public string? HeadRemote { get; set; }

    public bool Fork { get; set; }

    public bool? Mergeable { get; set; } = true;

    public string? ReviewDecision { get; set; }

    public ChecksState Checks { get; set; } = ChecksState.None;

    public string ChecksSha { get; set; } = string.Empty;

    public List<string> Failing { get; set; } = [];

    public Dictionary<string, string> Logs { get; set; } = [];

    public List<WorldThread> Threads { get; set; } = [];

    public int NextComment { get; set; } = 1;

    public int Reruns { get; set; }

    public int Stamp { get; set; }

    public string? MergedSha { get; set; }
}
