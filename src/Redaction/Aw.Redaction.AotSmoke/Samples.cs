namespace Aw.Redaction.AotSmoke;

/// <summary>Synthetic inputs, assembled from parts so no scanner sees a whole token in this source.</summary>
internal static class Samples
{
    private const string Body = "Ab3dE6gH9jK2mN5pQ8sT1vW4yZ7";

    /// <summary>Name and input of each sample, in output order.</summary>
    public static IReadOnlyList<(string Name, string Input)> All { get; } =
    [
        ("prose", "The build passed; commit 0123456789abcdef0123456789abcdef01234567."),
        ("bearer", "Authorization: Bear" + "er " + Body + Body),
        ("env", "CLIENT" + "_SECRET=" + Body + "9"),
        ("url", "https://deploy:" + Body + "@example.com/repo.git"),
        ("lookalike", "token g\u04BB" + "p_" + Body + "Ab3dE6gH9"),
        ("zwj", "x gh" + "p_Ab3dE6gH9jK2mN5p\u200D" + Body + " y"),
        ("escaped", "{\"note\":\"a\\ngh" + "p_" + Body + "Ab3dE6gH9\"}"),
        ("unterminated", "-----BEGIN RSA PRIV" + "ATE KEY-----\n" + Body + Body + "\n\nafter"),
    ];

    /// <summary>Name, extra patterns, match timeout and input of each extra-pattern sample, in output order: a
    /// configured identifier shape, refused entries (a malformed id, a Unicode-aware shorthand, an unparsable
    /// pattern) and a pathological pattern that times out and blocks.</summary>
    public static IReadOnlyList<(string Name, ExtraPattern[] Extras, TimeSpan Timeout, string Input)> Extra { get; } =
    [
        ("extra", [new("acme-customer-id", "ACME-[0-9]{6}")], Redactor.DefaultMatchTimeout, "order ACME-123456 shipped"),
        ("extra-refused", [new("acme/customer", "ACME-\\d{6}"), new("acme-ok", "ACME-[0-9")], Redactor.DefaultMatchTimeout, "unused"),
        ("extra-timeout", [new("slow", "^(?:a|aa)+$")], TimeSpan.FromMilliseconds(100), new string('a', 64) + "!"),
    ];
}
