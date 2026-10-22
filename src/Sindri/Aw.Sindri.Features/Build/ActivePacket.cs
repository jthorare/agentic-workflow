using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Build;

/// <summary>The phase packet in force, as the build's fact holds it: <c language="text">{ hash, dir, repo }</c>. 'repo' is a checkout of the repository the packet builds on, which aw-packets needs for PKT-BASE-001; 'step.build.prepare' sets it.</summary>
/// <param name="Hash">The packet's frozen hash.</param>
/// <param name="Dir">The packet directory.</param>
/// <param name="Repo">A directory inside the repository, or null when the fact names none.</param>
internal sealed record ActivePacket(string Hash, string Dir, string? Repo)
{
    internal static ActivePacket? From(JsonNode? fact) =>
        StepFacts.Field(fact, "hash") is { } hash && StepFacts.Field(fact, "dir") is { } dir ? new ActivePacket(hash, dir, StepFacts.Field(fact, "repo")) : null;

    internal JsonObject ToFact()
    {
        var fact = new JsonObject { ["hash"] = Hash, ["dir"] = Dir };
        if (Repo is not null)
        {
            fact["repo"] = Repo;
        }

        return fact;
    }
}
