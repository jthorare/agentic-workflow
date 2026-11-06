namespace Aw.Context.Testing.Linear;

/// <summary>The made-up data a <see cref="FakeLinear"/> starts with. A fixture member ('Fixture Viewer') always exists and is the viewer, the author of every comment.</summary>
public sealed class FakeLinearSeed
{
    private static readonly (string Name, string Type)[] _states =
        [("Backlog", "backlog"), ("Todo", "unstarted"), ("In Progress", "started"), ("Done", "completed"), ("Canceled", "canceled")];

    internal FakeLinearSeed()
    {
        _ = Member("Fixture Viewer");
    }

    internal LinearStore Store { get; } = new();

    /// <summary>Adds a team with the five default workflow states (Backlog, Todo, In Progress, Done, Canceled).</summary>
    /// <param name="key">The team key, 'ENG'.</param>
    /// <param name="name">The team name; the key when null.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Team(string key, string? name = null)
    {
        var team = new LinearRow(Store.NewId(), name ?? key, key);
        Store.Teams.Add(team);
        Store.States.AddRange(_states.Select(s => new LinearRow(Store.NewId(), s.Name, Type: s.Type, TeamId: team.Id)));
        return this;
    }

    /// <summary>Adds a member.</summary>
    /// <param name="name">The member's name.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Member(string name)
    {
        Store.Members.Add(new LinearRow(Store.NewId(), name));
        return this;
    }

    /// <summary>Adds a label.</summary>
    /// <param name="name">The label name.</param>
    /// <param name="teamKey">The team the label belongs to; null makes a workspace label.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Label(string name, string? teamKey = null)
    {
        var team = teamKey is null ? null : Store.Teams.Find(t => t.Key == teamKey) ?? throw new ArgumentException($"Seed the team '{teamKey}' before its labels.", nameof(teamKey));
        Store.Labels.Add(new LinearRow(Store.NewId(), name, TeamId: team?.Id));
        return this;
    }

    /// <summary>Adds a comment, by the viewer, to a seeded issue.</summary>
    /// <param name="issueIdentifier">The issue's identifier ('ENG-1').</param>
    /// <param name="body">The comment text.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Comment(string issueIdentifier, string body)
    {
        var issue = Store.FindIssue(issueIdentifier) ?? throw new ArgumentException($"Seed the issue '{issueIdentifier}' before its comments.", nameof(issueIdentifier));
        var now = Store.Now();
        Store.Comments.Add(new FakeLinearComment(Store.NewId(), issue.Id, body, Store.Viewer.Name, now, now));
        return this;
    }

    /// <summary>Adds a cycle.</summary>
    /// <param name="name">The cycle name.</param>
    /// <param name="teamKey">The team the cycle belongs to; null makes a cycle every team has.</param>
    /// <param name="startsAt">When it starts; two weeks per cycle from 2026-01-01 when null.</param>
    /// <param name="endsAt">When it ends; two weeks after it starts when null.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Cycle(string name, string? teamKey = null, DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null)
    {
        var team = teamKey is null ? null : Store.Teams.Find(t => t.Key == teamKey) ?? throw new ArgumentException($"Seed the team '{teamKey}' before its cycles.", nameof(teamKey));
        var start = startsAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(14 * Store.Cycles.Count);
        Store.Cycles.Add(new LinearRow(Store.NewId(), name, TeamId: team?.Id, StartsAt: Iso(start), EndsAt: Iso(endsAt ?? start.AddDays(14))));
        return this;
    }

    private static string Iso(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Adds a project.</summary>
    /// <param name="name">The project name.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Project(string name)
    {
        Store.Projects.Add(new LinearRow(Store.NewId(), name));
        return this;
    }

    /// <summary>Adds an issue to a seeded team (created now, by the fake's clock).</summary>
    /// <param name="teamKey">The team key.</param>
    /// <param name="title">The title.</param>
    /// <param name="more">The other columns, or null.</param>
    /// <returns>This seed.</returns>
    public FakeLinearSeed Issue(string teamKey, string title, FakeLinearIssueSeed? more = null)
    {
        more ??= new FakeLinearIssueSeed();
        var team = Store.Teams.Find(t => t.Key == teamKey) ?? throw new ArgumentException($"Seed the team '{teamKey}' before its issues.", nameof(teamKey));
        _ = Store.States.Find(s => s.TeamId == team.Id && s.Name == more.State) ?? throw new ArgumentException($"Team '{teamKey}' has no state '{more.State}'.", nameof(more));
        var now = Store.Now();
        Store.Issues.Add(new FakeLinearIssue(
            Store.NewId(), Store.NextIdentifier(teamKey), title, more.Description, more.State, more.Assignee, more.Priority, more.Estimate, more.Cycle, more.Project, more.Labels, teamKey, now, now, ArchivedAt: null));
        return this;
    }
}
