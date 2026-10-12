namespace Aw.Judge.Testing;

/// <summary>Fake secrets in real formats, built by concatenation so no scanner flags this file.</summary>
public static class SampleSecrets
{
    /// <summary>An Anthropic-style API key.</summary>
    public static string AnthropicKey { get; } = "sk-" + "ant-api03-" + "abcdefghijklmnopqrstuvwxyz012345";

    /// <summary>A GitHub personal access token.</summary>
    public static string GitHubToken { get; } = "gh" + "p_" + "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>An AWS access key id.</summary>
    public static string AwsKeyId { get; } = "AK" + "IA" + "ABCDEFGHIJKLMNOP";

    /// <summary>A Slack bot token.</summary>
    public static string SlackToken { get; } = "xo" + "xb-" + "1234567890-abcdefghij";

    /// <summary>A three-part JWT.</summary>
    public static string Jwt { get; } = "ey" + "JhbGciOiJIUzI1NiJ9" + "." + "eyJzdWIiOiIxMjM0NTY3ODkwIn0" + "." + "abcdefghijklmnop";

    /// <summary>A standalone 40-character lowercase hex run (a full git sha).</summary>
    public static string FortyHex { get; } = string.Concat(Enumerable.Repeat("0123456789abcdef", 3))[..40];

    /// <summary>The ones every IInputRedactor must remove (the contract's samples).</summary>
    public static IReadOnlyList<string> Contract { get; } = [AnthropicKey, GitHubToken, AwsKeyId];
}
