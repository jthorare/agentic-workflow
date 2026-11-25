using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'setup:' block (setup-once spec §7.1): what Sindri does on its own while the owner sets it up once. Left out of the canonical JSON when absent, so a profile
/// without it keeps the hash it was approved under.
/// </summary>
/// <remarks>
/// First-run keys (first-run spec §3.1, decision 15a), all scalars so `profile set` and the key writer can write them: <see cref="SkipAccounts"/> and <see cref="SkipFirstWorkflow"/> are `true` or absent,
/// and <see cref="Oauth"/> holds the client id of Sindri's OAuth app per provider. None holds a secret.
/// </remarks>
/// <param name="Mcp">'auto' (the default) lets Sindri register itself in each repo once the profile is approved; 'off' stops that, and the button still works.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetupSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mcp = null)
{
    /// <summary>`setup.skipAccounts`: the owner skipped the accounts question; the `sources` step then counts as done. Cleared when GitHub connects. Absent means not skipped.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SkipAccounts { get; init; }

    /// <summary>`setup.skipFirstWorkflow`: the owner dismissed the hello-sindri step; it then counts as done. Absent means not dismissed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SkipFirstWorkflow { get; init; }

    /// <summary>`setup.oauth.*`: the client id of Sindri's OAuth app per provider, replacing the id this build ships; null leaves the shipped one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SetupOauthSettings? Oauth { get; init; }

    /// <summary>Sindri registers itself in each repo (the default).</summary>
    public const string Auto = "auto";

    /// <summary>Sindri leaves the registration to the button.</summary>
    public const string Off = "off";

    /// <summary>Whether <paramref name="profile"/> lets Sindri register itself on its own: true unless 'setup.mcp' is 'off'.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>True when the automatic registration may run.</returns>
    public static bool McpAutomatic(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return !string.Equals(profile.Setup?.Mcp, Off, StringComparison.Ordinal);
    }
}
