using System.Text.Json.Nodes;

namespace Aw.Judge.Tests.Providers;

/// <summary>The real usage-limit strings (tests/fixtures/judge/usage-limit/limit-strings.json), the one file the matcher's tests share.</summary>
internal static class UsageLimitStrings
{
    private static readonly JsonNode _file = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UsageLimit", "limit-strings.json")))!;

    /// <summary>What Claude prints at its limit.</summary>
    public static IReadOnlyList<string> Claude { get; } = [.. _file["claude"]!.AsArray().Select(node => node!.GetValue<string>())];

    /// <summary>The Codex limit message.</summary>
    public static string CodexMessage { get; } = _file["codex"]!["message"]!.GetValue<string>();

    /// <summary>The Codex limit's codex_error_info.</summary>
    public static string CodexErrorInfo { get; } = _file["codex"]!["codexErrorInfo"]!.GetValue<string>();

    /// <summary>Every real string, for the theories.</summary>
    public static IEnumerable<string> All => [.. Claude, CodexMessage];

    /// <summary>The same strings as theory data.</summary>
    public static TheoryData<string> AllData => [.. All];
}
