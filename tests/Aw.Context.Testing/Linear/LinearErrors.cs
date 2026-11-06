using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// The error shapes the fake answers mutations with. The repeated-client-id shape is a (Ph0 checks) assumption: real
/// Linear's wording is unknown until the probe runs, so it lives here and nowhere else.
/// </summary>
internal static class LinearErrors
{
    public static LinearMutationException DuplicateId(string kind, string id) =>
        Invalid(
            "Argument Validation Error",
            $"{kind} id '{id}' is already used; a client id must be unique.",
            new JsonArray(new JsonObject { ["property"] = "id", ["constraints"] = new JsonObject { ["isUnique"] = $"id must be unique: the {kind} '{id}' already exists" } }));

    public static LinearMutationException NotFound(string kind, string id) =>
        Invalid("Entity not found: " + kind, $"Could not find referenced {kind} '{id}'.", validationErrors: null);

    public static LinearMutationException Invalid(string message, string present, JsonArray? validationErrors)
    {
        var extensions = new JsonObject
        {
            ["type"] = "invalid input",
            ["code"] = "INVALID_INPUT",
            ["userError"] = true,
            ["userPresentableMessage"] = present,
        };
        if (validationErrors is not null)
        {
            extensions["validationErrors"] = validationErrors;
        }

        return new LinearMutationException(new JsonObject { ["message"] = message, ["extensions"] = extensions });
    }
}
