using System.Globalization;

namespace Aw.Context.Testing.Linear;

/// <summary>The state of the fake Linear: its rows, issues, comments and attachments, a clock that moves one second per call and an id counter. Not thread-safe: <see cref="FakeLinear"/> holds its lock around every use.</summary>
internal sealed class LinearStore
{
    private static readonly DateTimeOffset _epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);
    private int _tick;
    private int _ids;

    public List<LinearRow> Teams { get; } = [];

    public List<LinearRow> States { get; } = [];

    public List<LinearRow> Members { get; } = [];

    public List<LinearRow> Labels { get; } = [];

    public List<LinearRow> Cycles { get; } = [];

    public List<LinearRow> Projects { get; } = [];

    public List<FakeLinearIssue> Issues { get; } = [];

    public List<FakeLinearComment> Comments { get; } = [];

    public List<FakeLinearAttachment> Attachments { get; } = [];

    /// <summary>Old identifier to issue id, for an issue that moved to another team.</summary>
    public Dictionary<string, string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? LastWrittenIssue { get; set; }

    /// <summary>The workspace's URL key, 'linear.app/&lt;key&gt;'.</summary>
    public string UrlKey { get; set; } = "fixture";

    public LinearRow Viewer => Members[0];

    public string NewId() => new Guid(++_ids, 0, 0x4000, [0x80, 0, 0, 0, 0, 0, 0, 1]).ToString();

    public string Now() => _epoch.AddSeconds(++_tick).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public string NextIdentifier(string teamKey)
    {
        var number = _numbers.GetValueOrDefault(teamKey) + 1;
        _numbers[teamKey] = number;
        return string.Create(CultureInfo.InvariantCulture, $"{teamKey}-{number}");
    }

    public FakeLinearIssue? FindIssue(string idOrIdentifier) =>
        Issues.Find(i => i.Id == idOrIdentifier || string.Equals(i.Identifier, idOrIdentifier, StringComparison.OrdinalIgnoreCase))
        ?? (Aliases.TryGetValue(idOrIdentifier, out var id) ? Issues.Find(i => i.Id == id) : null);

    public void Save(FakeLinearIssue issue)
    {
        Issues[Issues.FindIndex(i => i.Id == issue.Id)] = issue;
        LastWrittenIssue = issue.Id;
    }

    public static LinearRow? FindRow(List<LinearRow> rows, string? name) => rows.Find(r => r.Name == name);
}
