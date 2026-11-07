using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The five mutations' requests (Linear hub spec 2026-11-04 §5.2): the operation's text and its variables, nothing sent. A client id is the input's 'id'; an issue update carries only the fields that are set.</summary>
internal static class LinearWriteRequests
{
    public const string IssueCreate = "IssueCreate";
    public const string IssueUpdate = "IssueUpdate";
    public const string CommentCreate = "CommentCreate";
    public const string CommentUpdate = "CommentUpdate";
    public const string AttachmentLinkUrl = "AttachmentLinkUrl";

    public static LinearRequest CreateIssue(LinearIssueDraft draft, LinearClientId clientId)
    {
        JsonObject input = new() { ["id"] = clientId.Value, ["teamId"] = draft.TeamId.Value, ["title"] = draft.Title };
        Put(input, "description", draft.Description);
        Put(input, "stateId", draft.StateId?.Value);
        Put(input, "assigneeId", draft.AssigneeId?.Value);
        Put(input, "priority", draft.Priority);
        Put(input, "estimate", draft.Estimate);
        Put(input, "cycleId", draft.CycleId?.Value);
        Put(input, "projectId", draft.ProjectId?.Value);
        Put(input, "parentId", draft.ParentId?.Value);
        if (draft.LabelIds is { Count: > 0 } labels)
        {
            input["labelIds"] = Ids(labels);
        }

        return Request(IssueCreate, new JsonObject { ["input"] = input });
    }

    public static LinearRequest UpdateIssue(ExternalId issueId, LinearIssueChange change)
    {
        JsonObject input = [];
        Set(input, "title", change.Title.IsSet, JsonValue.Create(change.Title.Value));
        Set(input, "description", change.Description.IsSet, JsonValue.Create(change.Description.Value));
        Set(input, "stateId", change.StateId.IsSet, JsonValue.Create(change.StateId.Value.Value));
        Set(input, "assigneeId", change.AssigneeId.IsSet, JsonValue.Create(change.AssigneeId.Value?.Value));
        Set(input, "priority", change.Priority.IsSet, JsonValue.Create(change.Priority.Value));
        Set(input, "estimate", change.Estimate.IsSet, JsonValue.Create(change.Estimate.Value));
        Set(input, "cycleId", change.CycleId.IsSet, JsonValue.Create(change.CycleId.Value?.Value));
        Set(input, "projectId", change.ProjectId.IsSet, JsonValue.Create(change.ProjectId.Value?.Value));
        Set(input, "parentId", change.ParentId.IsSet, JsonValue.Create(change.ParentId.Value?.Value));
        if (change.LabelIds.IsSet)
        {
            input["labelIds"] = Ids(change.LabelIds.Value ?? []);
        }

        return Request(IssueUpdate, new JsonObject { ["id"] = issueId.Value, ["input"] = input });
    }

    public static LinearRequest CreateComment(ExternalId issueId, string body, LinearClientId clientId) =>
        Request(CommentCreate, new JsonObject { ["input"] = new JsonObject { ["id"] = clientId.Value, ["issueId"] = issueId.Value, ["body"] = body } });

    public static LinearRequest UpdateComment(ExternalId commentId, string body) =>
        Request(CommentUpdate, new JsonObject { ["id"] = commentId.Value, ["input"] = new JsonObject { ["body"] = body } });

    public static LinearRequest LinkUrl(ExternalId issueId, Uri url, string title) =>
        Request(AttachmentLinkUrl, new JsonObject { ["issueId"] = issueId.Value, ["url"] = url.AbsoluteUri, ["title"] = title });

    private static LinearRequest Request(string operation, JsonObject variables) => new(LinearOperations.Text(operation), variables);

    private static JsonArray Ids(IReadOnlyList<ExternalId> ids) => new([.. ids.Select(static i => (JsonNode?)JsonValue.Create(i.Value))]);

    private static void Put(JsonObject input, string name, string? value)
    {
        if (value is not null)
        {
            input[name] = value;
        }
    }

    private static void Put(JsonObject input, string name, int? value)
    {
        if (value is { } number)
        {
            input[name] = number;
        }
    }

    private static void Put(JsonObject input, string name, double? value)
    {
        if (value is { } number)
        {
            input[name] = number;
        }
    }

    private static void Set(JsonObject input, string name, bool isSet, JsonNode? value)
    {
        if (isSet)
        {
            input[name] = value;
        }
    }
}
