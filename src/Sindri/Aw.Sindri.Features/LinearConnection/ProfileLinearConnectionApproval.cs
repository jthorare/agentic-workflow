using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>
/// The one-click approval of a connection change, as the login save does it: when the latest approved profile plus exactly the keys this click wrote is the live profile (by hash), the save approves it, with the same
/// 'profile.approved' event and snapshot an approval records. When anything else waits (even a change the profile diff doesn't list, or a hand-edited url), or nothing was ever approved, nothing is approved.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="approval">Records the approval.</param>
/// <param name="parser">Checks and hashes the approved profile with the change applied.</param>
internal sealed class ProfileLinearConnectionApproval(IProfileSource live, ProfileChangeApproval approval, IProfileTextParser parser) : ILinearConnectionApproval
{
    public async ValueTask<Result<bool>> Settle(LinearConnectionChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError
            ? loaded.Error
            : await approval.Settle(loaded.Value, previous => IsApprovedPlus(previous, loaded.Value, change), cancellationToken).ConfigureAwait(false);
    }

    // The diff can miss a real change, so the check is on the hash: the approved profile with only these keys set must be exactly the live profile. The approval is a canonical snapshot with no YAML text, so the edit is made on
    // the typed profile's 'context' JSON.
    private bool IsApprovedPlus(LoadedProfile previous, LoadedProfile liveProfile, LinearConnectionChange change)
    {
        var context = Apply(previous.Profile.Context, change);
        var rechecked = parser.Recheck(previous.Profile with { Context = context }, previous.Source);
        return rechecked.IsSuccess && rechecked.Value.Hash == liveProfile.Hash;
    }

    private static JsonElement Apply(JsonElement? context, LinearConnectionChange change)
    {
        var root = context is { ValueKind: JsonValueKind.Object } existing && JsonNode.Parse(existing.GetRawText()) is JsonObject parsed ? parsed : [];
        var linear = Child(Child(root, "sources"), "linear");
        if (change.Token is { } token)
        {
            linear["token"] = token;
        }

        if (change.Teams is { } teams)
        {
            linear["teams"] = new JsonArray([.. teams.Select(static t => (JsonNode?)JsonValue.Create(t))]);
        }

        if (change.WriteToken is { } writeToken)
        {
            linear["writeToken"] = writeToken;
        }

        if (change.WriteTokenRemoved)
        {
            _ = linear.Remove("writeToken");
        }

        using var document = JsonDocument.Parse(root.ToJsonString());
        return document.RootElement.Clone();
    }

    private static JsonObject Child(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject child)
        {
            return child;
        }

        var created = new JsonObject();
        parent[name] = created;
        return created;
    }
}
