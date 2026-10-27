using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Build;

/// <summary>The phase packet in force, as the build's fact holds it: <c language="text">{ hash, dir, repo, startHead, rework }</c>. 'repo' is a checkout of the repository the packet builds on, which aw-packets needs for PKT-BASE-001; 'step.build.prepare' sets it, and 'startHead', the worktree's head when the round began (a rework round begins at the earlier round's code head); 'rework' is true when the round began with notes from a later station.</summary>
/// <param name="Hash">The packet's frozen hash.</param>
/// <param name="Dir">The packet directory.</param>
/// <param name="Repo">A directory inside the repository, or null when the fact names none.</param>
/// <param name="StartHead">The worktree's head when the round began, or null when the fact names none.</param>
/// <param name="Rework">Whether the round began with notes from a later station (a rework round).</param>
internal sealed record ActivePacket(string Hash, string Dir, string? Repo, string? StartHead = null, bool Rework = false)
{
    internal static ActivePacket? From(JsonNode? fact) =>
        StepFacts.Field(fact, "hash") is { } hash && StepFacts.Field(fact, "dir") is { } dir ? new ActivePacket(hash, dir, StepFacts.Field(fact, "repo"), StepFacts.Field(fact, "startHead"), fact is JsonObject o && o["rework"] is JsonValue r && r.TryGetValue<bool>(out var flag) && flag) : null;

    internal JsonObject ToFact()
    {
        var fact = new JsonObject { ["hash"] = Hash, ["dir"] = Dir };
        if (Repo is not null)
        {
            fact["repo"] = Repo;
        }

        if (StartHead is not null)
        {
            fact["startHead"] = StartHead;
        }

        if (Rework)
        {
            fact["rework"] = true;
        }

        return fact;
    }
}
