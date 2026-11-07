using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.ListLinearIssues;

namespace Aw.Sindri.Tests.LinearIssues;

/// <summary>Builders for the stored Linear issues the list tests read: made-up teams ENG and OPS, the viewer Joi Example and a teammate Sam Lee.</summary>
internal static class LinearIssueRows
{
    internal const string ViewerId = "00000000-0000-4000-8000-0000000000a1";
    internal const string SamId = "00000000-0000-4000-8000-0000000000a2";
    internal const string EngId = "00000000-0000-4000-8000-0000000000e1";
    internal const string OpsId = "00000000-0000-4000-8000-0000000000e2";

    internal static LinearPersonView Viewer { get; } = new(ViewerId, "Joi Example");

    internal static LinearPersonView Sam { get; } = new(SamId, "Sam Lee");

    internal static LinearSourceFacts Connected(LinearPersonView? viewer, LinearFeedView? feed = null) =>
        new(Connected: true, viewer, feed ?? new LinearFeedView("ok", KnownInstants.Epoch.AddMinutes(-2), Code: null, Reread: null));

    /// <summary>One open issue. The record key is made from the team and the number, so each (team, number) is one issue.</summary>
    internal static LinearIssueRowView Row(
        string key,
        LinearPersonView? assignee,
        int priority = 2,
        double updatedMinutesAgo = 10,
        string stateType = "started",
        string stateId = "00000000-0000-4000-8000-000000000052",
        string? labelId = null,
        string? projectId = null,
        string? cycleId = null)
    {
        var (teamKey, teamId, offset) = key.StartsWith("OPS", StringComparison.Ordinal) ? ("OPS", OpsId, 0x200) : ("ENG", EngId, 0x100);
        var number = int.Parse(key[(key.IndexOf('-', StringComparison.Ordinal) + 1)..], CultureInfo.InvariantCulture);
        return new LinearIssueRowView(
            key,
            "linear:issue/00000000-0000-4000-8000-" + (offset + number).ToString("x12", CultureInfo.InvariantCulture),
            "https://linear.app/acme/issue/" + key,
            "Title of " + key,
            new LinearTeamView(teamId, teamKey),
            new LinearStateView(stateId, stateType == "started" ? "In Progress" : stateType, stateType),
            priority,
            assignee,
            projectId is null ? null : new LinearNamedView(projectId, "Sync worker"),
            cycleId is null ? null : new LinearCycleView(cycleId, 14, "Cycle 14"),
            labelId is null ? [] : [new LinearLabelView(labelId, "bug", "#eb5757")],
            KnownInstants.Epoch.AddMinutes(-updatedMinutesAgo),
            Link: null,
            Pr: null);
    }
}
