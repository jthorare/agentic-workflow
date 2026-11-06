using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Records;

/// <summary>Synthetic Linear issue side rows (fields and comments) and the record writes that carry them, for the tests of the 'linear' side-row writer and of what reads its tables.</summary>
public static class LinearSideSamples
{
    /// <summary>The key of the sample issue.</summary>
    public const string Key = "linear:issue/00000000-0000-0000-0000-0000000000a1";

    /// <summary>An assignee.</summary>
    public static ExternalId Ada { get; } = ExternalId.From("user-ada");

    /// <summary>Another assignee.</summary>
    public static ExternalId Bo { get; } = ExternalId.From("user-bo");

    /// <summary>A workflow state.</summary>
    public static ExternalId Todo { get; } = ExternalId.From("state-todo");

    /// <summary>Another workflow state.</summary>
    public static ExternalId Doing { get; } = ExternalId.From("state-doing");

    /// <summary>The fields of the sample issue.</summary>
    /// <param name="assignee">The assignee.</param>
    /// <param name="state">The state.</param>
    /// <param name="updatedAt">When Linear last changed it.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The fields.</returns>
    public static LinearIssueFields Fields(ExternalId? assignee, ExternalId? state, DateTimeOffset? updatedAt = null, int priority = 2) => new(
        ExternalId.From("team-1"), "ENG-1", state, state == Doing ? "started" : "unstarted", assignee, priority, Estimate: 3, ExternalId.From("cycle-1"), ExternalId.From("project-1"), ParentId: null,
        [ExternalId.From("label-a"), ExternalId.From("label-b")], "2026-11-20", -10.5, updatedAt ?? RecordSamples.Now);

    /// <summary>A comment.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="body">Its text.</param>
    /// <param name="minutes">Minutes after <see cref="RecordSamples.Now"/> it was written.</param>
    /// <param name="author">Its author.</param>
    /// <param name="sindri">Whether it carries Sindri's marker.</param>
    /// <returns>The comment.</returns>
    public static LinearCommentRow Comment(string id, string body, int minutes = 0, ExternalId? author = null, bool sindri = false) =>
        new(ExternalId.From(id), author ?? Ada, RecordSamples.Now.AddMinutes(minutes), body, sindri);

    /// <summary>The side rows of an issue, newest comment first.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="written">The fields a write-through sent.</param>
    /// <param name="comments">The comments.</param>
    /// <returns>The rows.</returns>
    public static LinearSideRows Rows(LinearIssueFields fields, LinearWrittenFields written = LinearWrittenFields.None, params LinearCommentRow[] comments) =>
        new(new LinearIssueSide(fields, comments), written);

    /// <summary>A write of the sample issue's record at a version, carrying side rows.</summary>
    /// <param name="side">The side rows, or null for none.</param>
    /// <param name="version">The record's version.</param>
    /// <param name="key">The record's key.</param>
    /// <returns>The write.</returns>
    public static RecordWrite Write(LinearSideRows? side, string version = "v1", string key = Key) =>
        RecordSamples.Write(RecordSamples.Record(key, version), "words") with { Side = side };
}
