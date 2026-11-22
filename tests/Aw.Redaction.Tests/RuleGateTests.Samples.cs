namespace Aw.Redaction.Tests;

[Trait("Lane", "unit")]
public sealed partial class RuleGateTests
{
    // Samples per built-in rule id, one group per rule with that id, in BuiltinRules.All order (secret-assignment is
    // seven rules), one sample per branch the gate names (assembled from parts, like the corpus, so no scanner sees a
    // whole token here).
    private static readonly Dictionary<string, string[][]> _samples = new(StringComparer.Ordinal)
    {
        ["private-key"] =
        [
            [Shapes.Pem("-----END RSA PRIV" + "ATE KEY-----"), Shapes.Pem(string.Empty)],
        ],
        ["anthropic-key"] =
        [
            [Shapes.Join("sk-", "ant-", Shapes.Body)],
        ],
        ["openai-key"] =
        [
            [Shapes.Join("sk-", Shapes.Body), Shapes.Join("sk-", "proj-", Shapes.Body)],
        ],
        ["aws-access-key"] =
        [
            [Shapes.Aws("AK" + "IA"), Shapes.Aws("AS" + "IA")],
        ],
        ["github-token"] =
        [
            [.. "pousr".Select(c => Shapes.Join("gh", c + "_", Shapes.Body, "Ab3dE6gH9")), Shapes.Join("github", "_pat_", Shapes.Body)],
        ],
        ["slack-token"] =
        [
            [.. "abprs".Select(c => Shapes.Join("xo", "x" + c + "-1234567890-", Shapes.Body))],
        ],
        ["stripe-key"] =
        [
            [Shapes.Join("sk", "_live_", Shapes.Body), Shapes.Join("rk", "_test_", Shapes.Body)],
        ],
        ["google-api-key"] =
        [
            [Shapes.Join("AI", "za", Shapes.Body, "Ab3dE6gH")],
        ],
        ["linear-key"] =
        [
            [Shapes.Join("lin", "_api_", Shapes.Body, Shapes.Body), Shapes.Join("lin", "_oauth_", Shapes.Body, Shapes.Body)],
        ],
        ["jwt"] =
        [
            [Shapes.Join("ey", "J", Shapes.Body, ".", Shapes.Body, ".", Shapes.Body)],
        ],
        ["google-oauth-token"] =
        [
            [Shapes.Join("ya", "29.", Shapes.Body)],
        ],
        ["bearer"] =
        [
            [Shapes.Join("Authorization: Bear", "er ", Shapes.Body)],
        ],
        ["basic-auth"] =
        [
            [Shapes.Join("Authorization: Bas", "ic dXNlcjpw", "YXNzd29yZDEyMzQ=")],
        ],
        ["credentialed-url"] =
        [
            [Shapes.Join("https://deploy:", Shapes.Body, "@example.com/repo.git")],
        ],
        ["secret-assignment"] =
        [
            [
                Shapes.Join("{\"api", "_key\": \"", Shapes.Body, "\"}"), Shapes.Join("client", "_secret = '", Shapes.Body, "'"),
                Shapes.Join("my", "Token: \"", Shapes.Body, "\""), Shapes.Join("db_pass", "word=\"", Shapes.Body, "\""),
                Shapes.Join("private", "_key: '", Shapes.Body, "'"), Shapes.Join("access", "Key = \"", Shapes.Body, "\""),
            ],
            [
                Shapes.Join("API", "_KEY=", Shapes.Body), Shapes.Join("CLIENT", "_SECRET=", Shapes.Body), Shapes.Join("GH_TOK", "EN=", Shapes.Body),
                Shapes.Join("DB_PASS", "WORD=", Shapes.Body), Shapes.Join("PRIVATE", "_KEY=", Shapes.Body), Shapes.Join("ACCESS", "_KEY=", Shapes.Body),
            ],
            [
                Shapes.Join("cd repo && api", "_key=", Shapes.Body), Shapes.Join("x secret", "=", Shapes.Body), Shapes.Join("x tok", "en=", Shapes.Body),
                Shapes.Join("x pass", "word=", Shapes.Body), Shapes.Join("x private", "_key=", Shapes.Body), Shapes.Join("x access", "_key=", Shapes.Body),
            ],
            [
                Shapes.Join("https://x/cb?access", "_token=", Shapes.Body), Shapes.Join("https://x/cb#tok", "en=", Shapes.Body),
                Shapes.Join("https://x/?a=1&api", "_key=", Shapes.Body),
            ],
            [Shapes.Join("X-Api", "-Key: ", Shapes.Body), Shapes.Join("X-Auth-Tok", "en: ", Shapes.Body), Shapes.Join("X-Client-Sec", "ret: ", Shapes.Body)],
            [Shapes.Join("  client", "_secret: ", Shapes.Body, "  # rotated"), Shapes.Join("db.pass", "word = ", Shapes.Body)],
            [Shapes.Join("aws_secret", "_access_key = ", Shapes.Body), Shapes.Join("aws_session", "_token=", Shapes.Body)],
        ],
        ["ssn"] =
        [
            [Shapes.Join("SSN 123-", "45-6789 on file")],
        ],
        ["mrn"] =
        [
            [Shapes.Join("MR", "N: 12345678")],
        ],
    };
}
