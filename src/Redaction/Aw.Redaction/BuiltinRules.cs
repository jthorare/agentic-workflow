using System.Text.RegularExpressions;
using static Aw.Redaction.RuleGateClause;

namespace Aw.Redaction;

/// <summary>
/// The one pattern set (spec §4c): the union of judge's redactSecrets and sindri's scrub patterns, with the gaps the
/// A3 differential found closed. Order matters: when two rules overlap at the same start, the earlier one names the
/// hit. Patterns use explicit ASCII classes and lookarounds instead of \b, \s and \d (which are Unicode-aware in
/// .NET). Deliberately absent: judge's bare 40-hex rule, which erases git SHAs and digests and matches no secret
/// format.
/// </summary>
internal static class BuiltinRules
{
    // A token starts where no word character precedes it (\b without Unicode word characters).
    private const string Start = "(?<![A-Za-z0-9_])";
    private const string End = "(?![A-Za-z0-9_])";

    // The value of a KEY=value pair: 20+ token characters with a digit in the first 256 (so identifiers and words
    // don't count; bounded, so a long run stays linear).
    private const string Value = "(?=[A-Za-z0-9+/=_-]{0,255}?[0-9])(?<v>[A-Za-z0-9+/=_-]{20,})";

    // Whitespace as JavaScript's \s covers it in ASCII: a value may follow on the next line (the TS patterns allow it).
    private const string Ws = "[ \t\r\n\f\v]";

    // A PEM or PGP armor label: RSA PRIVATE KEY, OPENSSH PRIVATE KEY, PGP PRIVATE KEY BLOCK.
    private const string KeyLabel = "[A-Z ]{0,40}PRIVATE KEY(?: BLOCK)?-----";

    // Between two runs of an unterminated key body: one line break (never a blank line) or spaces.
    private const string BodyBreak = "[ \t]*\r?\n|[ \t]+";

    // A Value that may also hold dots (a dotted token in a shell pair).
    private const string DottedValue = "(?=[A-Za-z0-9+/=_.-]{0,255}?[0-9])(?<v>[A-Za-z0-9+/=_.-]{20,})";

    /// <summary>A field, header or attribute name whose value is a secret whatever it looks like.</summary>
    public const string SecretName =
        "^(?:[A-Za-z0-9_.-]*?" + SecretWord + "|(?:[A-Za-z0-9_-]*\\.)*(?:proxy-)?authorization|(?:[A-Za-z0-9_-]*\\.)*(?:set-)?cookie)$";

    private const string SecretWord = "(?:API[_-]?KEY|SECRET(?:[_-]?KEY)?|TOKEN|PASSWORD|PRIVATE[_-]?KEY|ACCESS[_-]?KEY)";

    // Gate clauses (RuleGate): every rule's gate names only what each of its matches must contain. SecretWord's
    // alternatives each hold one of these stems (API_KEY holds API, PRIVATE-KEY holds PRIVATE, ACCESSKEY holds ACCESS).
    private static readonly RuleGateClause SecretStem = AnyCase("api", "secret", "token", "password", "private", "access");
    private static readonly RuleGateClause Digit = AnyChar("0123456789");
    private static readonly RuleGateClause Separator = AnyChar("=:");

    /// <summary>The rules, in priority order. A new or changed pattern needs its gate derived again (RuleGateTests).</summary>
    public static IReadOnlyList<RedactionRule> All { get; } =
    [
        // A terminated block (the END scan stops at the next BEGIN, so a run of BEGIN lines stays linear), or, when no
        // END line follows (a truncated key), the armor headers, one blank line after them, and the base64 body after
        // BEGIN: on the BEGIN line, on following lines of any length, or flattened onto one line with spaces. The body
        // needs 16+ base64 characters, and each later run 16+ too (base64 lines are 64 or 76 long), so a BEGIN line followed
        // by prose, or the short lines of a log after a truncated key, are left alone. Runs are unbounded in number: they
        // are separated by whitespace, so they can't overlap and the scan stays linear.
        new("private-key",
            "-----BEGIN " + KeyLabel + "(?:(?:(?!-----BEGIN )(?s:.)){0,16384}?-----END " + KeyLabel
            + "|(?:[ \t]*\r?\n[A-Za-z][A-Za-z-]{0,39}:[^\r\n]{0,200}){0,8}(?:[ \t]*\r?\n(?=[ \t]*\r?\n))?"
            + "(?:" + BodyBreak + ")?(?=[A-Za-z0-9+/=]{16})[A-Za-z0-9+/=]{1,16384}(?:(?:" + BodyBreak + ")[A-Za-z0-9+/=]{16,16384})*)",
            Gate: new(Exact("-----BEGIN "))),
        new("anthropic-key", Start + "sk-ant-[A-Za-z0-9_-]{20,}", Gate: new(Exact("sk-ant-"))),
        new("openai-key", Start + "sk-(?:proj-)?[A-Za-z0-9_-]{20,}", Gate: new(Exact("sk-"))),
        new("aws-access-key", Start + "A(?:KIA|SIA)[0-9A-Z]{16}" + End, Gate: new(Exact("AKIA", "ASIA"))),
        new("github-token", Start + "(?:gh[pousr]_[A-Za-z0-9_]{30,}|github_pat_[A-Za-z0-9_]{22,})",
            Gate: new(Exact("ghp_", "gho_", "ghu_", "ghs_", "ghr_", "github_pat_"))),
        new("slack-token", Start + "xox[abprs]-[A-Za-z0-9-]{10,}", Gate: new(Exact("xox"))),
        new("stripe-key", Start + "[sr]k_(?:live|test)_[A-Za-z0-9]{16,}", Gate: new(Exact("k_live_", "k_test_"))),
        new("google-api-key", Start + "AIza[0-9A-Za-z_-]{35}" + End, Gate: new(Exact("AIza"))),
        new("linear-key", Start + "lin_(?:api|oauth)_[A-Za-z0-9]{32,}", Gate: new(Exact("lin_api_", "lin_oauth_"))),
        // Atomic segments: each scan ends at the next dot, so an unbounded segment (an 8 KB identity token) stays linear; a
        // match starts only where a run of segment characters starts (so a chain of "-eyJ" can't make it quadratic), and the
        // whole run is the first segment when an eyJ starts a word inside it (${AUTH:-eyJ...}, session-eyJ...).
        new("jwt", "(?<![A-Za-z0-9_-])(?=[A-Za-z0-9_-]*?(?<![A-Za-z0-9_])eyJ)(?>[A-Za-z0-9_-]{8,})\\.(?>[A-Za-z0-9_-]{8,})\\.[A-Za-z0-9_-]{8,}",
            Gate: new(Exact("eyJ"), Exact("."))),
        new("google-oauth-token", Start + "ya29\\.[A-Za-z0-9_-]{20,}", Gate: new(Exact("ya29."))),
        new("bearer", Start + "Bearer" + Ws + "+(?<v>[A-Za-z0-9._~+/=-]{20,})", RegexOptions.IgnoreCase,
            new(AnyCase("bearer"))),
        // HTTP Basic credentials (a reversible user:password) after the header name, bare or quoted as in a JSON, JS or
        // Python header object, so the word "Basic" in prose never matches.
        new("basic-auth",
            "(?<![A-Za-z0-9_-])(?:proxy-)?authorization['\"]?[ \t]*[:=][ \t]*[\\['\"]{0,2}[ \t]*Basic[ \t]+(?<v>[A-Za-z0-9+/]{8,}={0,2})", RegexOptions.IgnoreCase,
            new(AnyCase("authorization"), AnyCase("basic"))),
        new("credentialed-url",
            Start + "[a-z][a-z0-9+.-]{0,30}://[^ \t\r\n/:@]{0,256}:(?<v>[^ \t\r\n/]{1,256})@(?=[^ \t\r\n/@]*(?:[/?# \t\r\n]|$))",
            RegexOptions.IgnoreCase, new(Exact("://"), Exact("@"))),

        // Code style: NAME [=:] "value", NAME ending in a secret word in any case; only a quoted value counts, so
        // member access and calls never do (refreshToken: RefreshTokenSchemaV2).
        new("secret-assignment",
            "(?<![A-Za-z0-9_-])[A-Za-z0-9_-]*?" + SecretWord + "['\"]?" + Ws + "*[=:]" + Ws + "*['\"]" + Value, RegexOptions.IgnoreCase,
            new(SecretStem, Separator, AnyChar("'\""), Digit)),

        // Env style: an UPPER_SNAKE name may take an unquoted value, dots included (API_KEY=..., CLIENT_SECRET=...).
        new("secret-assignment",
            "(?<![A-Za-z0-9_-])(?:[A-Z0-9]+_)*(?:API_?KEY|SECRET(?:_KEY)?|TOKEN|PASSWORD|PRIVATE_KEY|ACCESS_KEY)" + Ws + "*[=:]" + Ws + "*['\"]?"
            + DottedValue,
            Gate: new(Exact("API", "SECRET", "TOKEN", "PASSWORD", "PRIVATE_KEY", "ACCESS_KEY"), Separator, Digit)),

        // Shell style: name=value at a line start or after a space, ;, |, (, comma, quote, {, [ or : (a scoped .npmrc
        // //host/:_authToken=...), in any case (cd repo; export
        // api_key=..., opts=a,token=...). Keyword arguments holding attribute access (token=settings.value) don't count:
        // the value stops at a dot.
        new("secret-assignment",
            "(?<![^ \t\r\n;|(,\"'{\\[:])[A-Za-z0-9_.-]*?" + SecretWord + "=['\"]?" + Value, RegexOptions.IgnoreCase,
            new(SecretStem, Exact("="), Digit)),

        // Query style: name=value right after ?, & or # (?access_token=..., a #access_token= redirect fragment); the value
        // may hold dots.
        new("secret-assignment", "(?<=[?&#])[A-Za-z0-9_.-]*?" + SecretWord + "=" + DottedValue, RegexOptions.IgnoreCase,
            new(AnyChar("?&#"), SecretStem, Exact("="), Digit)),

        // Header style: a hyphenated header name may take an unquoted value (X-Api-Key: ...).
        new("secret-assignment",
            "(?<![A-Za-z0-9_-])(?:[A-Za-z0-9]+-)+(?:API-?KEY|SECRET|TOKEN|PASSWORD|ACCESS-?KEY|PRIVATE-?KEY):" + Ws + "*" + Value,
            RegexOptions.IgnoreCase, new(Exact("-"), SecretStem, Exact(":"), Digit)),

        // Config style (INI, YAML, .properties, .env in any case): a line that starts with the name may take an
        // unquoted value, which must end the line (or meet a comment), so code such as `secret = hash_v2(input)` or
        // `token = session.value` never matches.
        new("secret-assignment",
            "^[ \t]*(?:export[ \t]+)?(?:[A-Za-z0-9]+[_.-])*" + SecretWord + "[ \t]*[=:][ \t]*" + Value
            + "(?=[ \t]*(?:[#;][^\r\n]*)?\r?$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline, new(SecretStem, Separator, Digit)),

        // ~/.aws/credentials (INI): these fixed keys at a line start take an unquoted value.
        new("secret-assignment",
            "^[ \t]*(?:aws_secret_access_key|aws_session_token)[ \t]*[=:][ \t]*(?<v>[A-Za-z0-9+/=]{16,})",
            RegexOptions.IgnoreCase | RegexOptions.Multiline, new(AnyCase("aws_secret_access_key", "aws_session_token"), Separator)),

        new("ssn", Start + "[0-9]{3}-[0-9]{2}-[0-9]{4}" + End, Gate: new(Exact("-"), Digit)),
        new("mrn", Start + "MRN(?:" + Ws + "|[:#-])*(?<v>[0-9]{6,10})" + End, RegexOptions.IgnoreCase,
            new(AnyCase("mrn"), Digit)),
    ];
}
