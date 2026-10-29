namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// Secret pointers Sindri's parser (Aw.Sindri.Core.Secrets.SecretPointer.Parse) accepts, including targets with
/// spaces. Aw.Sindri.Tests checks that Sindri's parser accepts every entry and
/// that Context's 'context.sources.linear.token' check does too. Context's check
/// is only "a known scheme and a non-empty target", so it may accept more than Sindri does: Sindri's parser stays the
/// authority and doctor names the exact SND-SECRET reason.
/// </summary>
public static class SecretPointerSamples
{
    /// <summary>Pointers both checks accept.</summary>
    public static IReadOnlyList<string> Accepted { get; } =
    [
        "env:SLACK_TOKEN",
        "env:_x1",
        "file:/home/me/.slack-token",
        "file:/Users/me/Library/Application Support/sindri/slack",
        "file:/tmp/odd\tname",
        "keychain:slack/sindri-slack",
        "keychain:Slack Keys/sindri read",
        "op:Work/Slack/credential",
        "op:Private Vault/Slack/credential",
    ];
}
