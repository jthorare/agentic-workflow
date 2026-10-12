# Aw.Redaction

The one secret-pattern set (spec §4c "One pattern source", §4e B10). Tier 0, sublevel 0 in `deps.yaml`; the BCL
only. It replaces the two TS copies (judge `src/redact.ts`, sindri `src/scrub/patterns.ts`) when those packages are
ported, and the secret patterns of `config/hooks/detect-secrets.sh` when the hooks move to the C# binaries (spec §4e);
the TS scrubbers and the hook stay frozen until then. `Aw.Kernel.Telemetry`'s scrubbing processor (readiness stage T2)
is its first C# consumer, on every log and export path.

## API

```csharp
IRedactor redactor = Redactor.Default;            // built-in rules, 1 s match timeout per pattern per 128 K characters
ScrubResult result = redactor.Scrub(text);
// result.Text     scrubbed text; each hit becomes [REDACTED:<rule>]
// result.Hits     RedactionHit(Rule, Start, End) in input order: spans in the original text, never the value
// result.Outcome  Clean (Text is the input, same instance) | Redacted | Blocked
// result.BlockCode AW-REDACT-001 when Blocked
bool whole = redactor.IsSecretName("client_secret");   // true: write ScrubResult.SecretFieldText instead of the value
```

- `Redactor.Create(TimeSpan)` gives the same rules with another per-pattern timeout; `Redactor.Create(extraPatterns,
  TimeSpan, TimeProvider)` adds patterns from configuration ("Extra patterns from configuration" below).
- `IsSecretName(name)` is for callers that see a key and its value apart (a telemetry attribute, a header, a JSON
  field): the assignment rules need the name and the value in one string, so such a caller redacts the whole value of
  a field whose name ends in a secret word (API_KEY, SECRET, TOKEN, PASSWORD, PRIVATE_KEY, ACCESS_KEY, any case and
  separator) or is `Authorization`, `Proxy-Authorization`, `Cookie` or `Set-Cookie` (also with a dotted prefix, as in the
  OpenTelemetry attribute `http.request.header.cookie`), writing
  `ScrubResult.SecretFieldText` (`[REDACTED:secret-field]`, which later scrubs treat as a placeholder) in its place, and
  scrubs every other value.
- Register `IRedactor` as a singleton (`Redactor.Default`) in the composing `Add…()` method; tests use the real
  redactor (it has no I/O, so it needs no fake).

## Extra patterns from configuration

A profile can add identifier shapes the built-in rules don't know (context spec D14: Sindri's
`context.scrub.extraPatterns`, such as `{ id: acme-customer-id, pattern: "ACME-[0-9]{6}" }`). Redaction owns the
validation and the compiling, so there is still one scrubber; the caller only maps the errors to its own key path.

```csharp
RedactorResult created = Redactor.Create(
    [new ExtraPattern("acme-customer-id", "ACME-[0-9]{6}")], Redactor.DefaultMatchTimeout, clock);   // the host's TimeProvider
if (created.IsValid)
{
    created.Redactor.Scrub("order ACME-123456");   // "order [REDACTED:acme-customer-id]"
}
else
{
    foreach (var e in created.Errors) { /* e.Index, e.Field (Id | Pattern), e.Code, e.Message */ }
}
```

- **Scrubbing.** The accepted patterns run after the built-in rules, in the order given, compiled into the same size
  tiers with the redactor's match timeout and the options every rule gets (`CultureInvariant`, `ExplicitCapture`;
  write `(?i)` for case-insensitive). So they behave like built-in rules: a hit becomes `[REDACTED:<id>]`, a group
  named `v` limits the redaction to that group, the folded pass sees through the evasions below, their ids join
  `RuleIds` (so their placeholders are skipped and scrubbing stays idempotent), overlapping hits merge (a built-in
  rule names the merged span when its hit starts at the same character; otherwise the earlier-starting hit does, and
  the whole span is redacted either way), and a match that runs past the timeout blocks the whole text
  (`AW-REDACT-001`). Nothing checks a pattern for catastrophic backtracking up front; the timeouts are that guard.
- **Whole-scan deadline.** The match timeout restarts on every match, and configured patterns, unlike the built-in
  ones, aren't reviewed to stay linear: a pattern that is slow on every match would never time out and only stall.
  So a redactor with at least one extra pattern also gives each scrub a budget of `Redactor.ScanBudgetFactor` (4)
  times its size tier's match timeout, read from the `TimeProvider` passed to `Create` (Redaction reads no clock of
  its own) and checked around the placeholder scan, before every rule's scan and after every match. The regex work
  between two checks is one match call or the built-in, linear placeholder scan, so a scrub's regex work ends within
  about the budget plus one match timeout (the fold, merge and replace are linear and outside it); past it, the scrub
  blocks with `AW-REDACT-001` like a match timeout. The budget pays for every rule's scan in both passes, the
  built-in ones too, so keep `matchTimeout` at the default unless the inputs are small. Pass the host's real
  `TimeProvider`: a fake that doesn't advance turns the budget off.
  `Redactor.Default`, `Create(TimeSpan)` and `Create([], …)` have no deadline and keep the per-match timeout only.
- **Writing a pattern.** Only non-empty matches (or a non-empty `v` group) are redacted, so a pattern made only of
  lookarounds (`(?<=customer=)`) is accepted but redacts nothing: put the value inside it (`customer=(?<v>[0-9]{6})`).
  Give it boundaries the way the built-in rules do, for example
  `(?<![A-Za-z0-9_])ACME-[0-9]{6}(?![0-9])`: without them `ACME-[0-9]{6}` also matches inside `XACME-1234567` and
  leaves the 7. Avoid unbounded runs that can restart at every position of a long run (`[A-Z]+-[0-9]+`): a slow
  pattern blocks the text (it never leaks it), so an ordinary large input could be withheld.
- **Where to call it.** `ARCH-DIR-005` keeps `Aw.Redaction` out of a package's `Core` and `Features`, so the caller
  builds the redactor and maps the refusals in `Infrastructure` (or `Host`): the same list builds the `IRedactor`
  and is checked at profile validation, and `Core` and `Features` see only the mapped Kernel `Error`. The result is
  `RedactorResult`, not Kernel's `Result<T>`, because Redaction sits beside `Aw.Kernel` at tier 0 and can't
  reference it. The host registers that one redactor as the `IRedactor` singleton and passes the same instance to
  `TelemetryOptions.Redactor`, so log lines and spans are scrubbed with the extra patterns too (D14: one scrubber).
- **Refusals.** `Create` checks every entry and returns every refused field, in entry order, as an
  `ExtraPatternError(Index, Field, Code, Message)`; no redactor is built while one is refused. The message says how to
  fix the entry and never quotes the pattern. A caller reports the key path itself (Sindri: `SND-CONTEXT-001` at
  `context.scrub.extraPatterns[<Index>].id` or `.pattern`; an empty entry is reported on `Id`, with a message saying
  the entry is empty).

| Code | Field | Refused when |
|---|---|---|
| `AW-REDACT-002` | `Id` | an empty (null) entry, or an id that is null, not lowercase ASCII letters, digits and single hyphens starting with a letter, or over 64 characters (no `/`, `_`, spaces or `]`: the id sits in the placeholder); a built-in rule id, `blocked` or `secret-field`; the same id as an earlier entry |
| `AW-REDACT-003` | `Pattern` | null, empty or over 1024 characters; not a valid .NET regex (the message gives the parse error and offset); matches the empty text (`[A-Z]*`, `(…)?`: it would match at every position) or times out trying to; uses `\d`, `\w`, `\s`, `\b` or their negations, which are Unicode-aware in .NET, or a Unicode category `\p{…}`/`\P{…}` (the message gives the offset and the ASCII class to write instead). The check is lexical: an escaped backslash (`\\d`) is a backslash and a `d` and is accepted, while such an escape inside a class (`[\b]`, a backspace there) or an `(?x)` comment is refused too |

A null list or clock, or a timeout that isn't positive, is a bug and throws (`ArgumentNullException`,
`ArgumentOutOfRangeException`). The list's length isn't capped: every entry runs on every scrub, so keep it to the
identifier shapes that matter. `(?i)` folds case with the invariant culture, so it also matches a few non-ASCII
letters (the Kelvin sign for `k`); that only redacts more.

## Rules

| Rule id | Catches |
|---|---|
| `private-key` | `-----BEGIN … PRIVATE KEY-----` (PEM, OpenSSH) and `-----BEGIN PGP PRIVATE KEY BLOCK-----` through the matching END line; without an END line (a truncated key), up to 8 armor header lines (`Proc-Type:`, `DEK-Info:`, `Charset:`, …), one blank line after them, and the base64 body: on the BEGIN line, on following lines of any length and number, or flattened onto one line with spaces. The first body run and each later one need 16+ base64 characters, so a BEGIN line followed by prose, or the short log lines after a truncated key, are left alone |
| `anthropic-key`, `openai-key` | `sk-ant-…`, `sk-…`, `sk-proj-…` |
| `aws-access-key` | `AKIA…` and `ASIA…` (STS) access key ids |
| `github-token` | `ghp_`, `gho_`, `ghu_`, `ghs_`, `ghr_`, `github_pat_` |
| `slack-token`, `stripe-key`, `google-api-key`, `linear-key`, `jwt` | `xox[abprs]-`, `sk_live_`/`rk_test_`…, `AIza…`, `lin_api_`/`lin_oauth_`, three-part `eyJ…` tokens of any length |
| `google-oauth-token` | `ya29.…` access tokens, anywhere (so a dotted one in a lowercase pair is still caught) |
| `basic-auth` | the credentials after `Authorization: Basic` (also `Proxy-Authorization`, and a quoted name or value as in a JSON, JS or Python header object) |
| `bearer` | the token after `Bearer` (the word stays; the token may follow on the next line) |
| `credentialed-url` | the password in `scheme://user:password@host` (the user may be empty: `redis://:password@host`), up to the last `@` before the host (an unencoded `@` in the password stays inside the redaction) |
| `secret-assignment` | the value of `NAME = "value"` (NAME ends in API_KEY, SECRET, TOKEN, PASSWORD, PRIVATE_KEY, ACCESS_KEY; any case, quoted value), `UPPER_SNAKE=value` (quoted or not, dots allowed in the value), a shell pair `name=value` at a line start or after a space, `;`, `|`, `(`, comma, quote, `{`, `[` or `:` (`export api_key=…`, `curl -d "client_secret=…"`, `{access_token=…, expires_in=…}`, `Creds[token=…]`, a scoped `.npmrc` `//host/:_authToken=…`; the value stops at a dot, so `token=settings.value` keyword arguments stay), a query pair right after `?`, `&` or `#` (`?access_token=…`, a `#access_token=` redirect fragment; the value may hold dots), a config line in any case that starts with the name and ends with an unquoted value (INI, YAML, `.properties`, `.env`; `api_key=…`, `client_secret: …`, `db.password = …`), `X-Api-Key: value` headers, and `aws_secret_access_key` / `aws_session_token` lines. A value needs 20+ characters with a digit in its first 256 (the `aws_*` lines: 16+ base64 characters, no digit needed); in the code, env and header forms it may follow the separator on the next line |
| `ssn`, `mrn` | `123-45-6789`, `MRN: 12345678` |

Overlapping hits merge into one placeholder named by the earlier rule (`Redactor.RuleIds` is the priority order). A
`[REDACTED:<rule>]` placeholder naming one of these rules is never matched again, so scrubbing is idempotent; a crafted
placeholder holding anything else is still scanned. Not a rule: bare 40+ hex runs (judge's TS rule), which erase git
SHAs and digests and match no secret format.

## Evasions it folds away

The rules run twice: on the text as written and on a folded copy. Hits from the folded pass are mapped back to the
original spans, the two sets are united, and only the original is rewritten. The plain pass keeps the token
boundaries the fold would change (Cyrillic prose glued to a token, a fullwidth letter right before it, a Windows path
such as `C:\keys\rk_live_…` whose backslash-r the fold would decode). The fold:

- zero-width characters (U+200B–U+200D, U+2060–U+2064, U+FEFF, U+180E) and soft hyphens are dropped, so a joiner
  inside a token doesn't split it;
- Unicode spaces (NBSP, the U+2000 block, U+2028/U+2029) become a space; fullwidth ASCII (U+FF01–U+FF5E) and Cyrillic and Greek
  letters that look like Latin ones become ASCII (`gһp_`, `ΑΚΙΑ`);
- JSON escapes (`\n`, `\r`, `\t`, `\"`, `\/`, `\\`, `\uXXXX`) are decoded, so a token right after an escaped newline
  in serialized JSON still starts a word, and an escaped `client_secret` pair still matches.

Not folded: mathematical alphanumeric look-alikes (astral code points) and base64-wrapped secrets.

## Narrower than the hook, on purpose

`detect-secrets.sh` blocks any `NAME=value` with a 20+ character value anywhere in a command, digits or not, and with
no boundary before the name. Here a value needs a digit (logs and code are full of long all-letter identifiers), so
`export TOKEN=<30 letters>` stays, and a pair needs one of the boundaries the `secret-assignment` row lists before its
name;
`PassThroughTests` pins the first choice. The hook's other shapes (AWS keys, GitHub tokens, bearer tokens, and pairs
with a digit: `cd repo && api_key=…`, `curl -d "client_secret=…"`, `FOO=1 mytoken=…`, `x=1; Password=…`, dotted
values in UPPER_SNAKE and query pairs) are covered by corpus cases. Also narrower: a dotted value in a lowercase shell or
config pair (`access_token=ab12.cd34…`) is left alone, so keyword arguments that hold attribute access stay; the dotted
token shapes that matter (JWTs, `ya29.`) have their own rules.

## Timeouts block

Every pattern is a `Regex` with a match timeout. The timeout is a budget per window of input: up to 128 K characters
get the redactor's timeout (1 s for `Default`), and each doubling of the text doubles it (the rules are compiled once per
size tier), so a large ordinary input is scrubbed and only a pathological match blocks. A timeout returns `ScrubOutcome.Blocked` with
`Text = "[REDACTED:blocked]"` and `BlockCode = "AW-REDACT-001"`: the input is withheld, never passed on. The caller
logs the block (Redaction has no logger) and refuses the commit, write or egress.

The budget is per match call: .NET restarts the timeout on every match, so it does not bound a whole scan. Every built-in
rule is therefore written to stay linear on its own (no rule can restart at every position of a long run; see
`AdversarialInputTests`). Configured patterns aren't, so a redactor built with extra patterns also has a whole-scan
deadline ("Extra patterns from configuration").

## Pattern rules

Explicit ASCII classes (`[0-9]`, `[A-Za-z0-9_]`) and lookarounds instead of `\d`, `\s`, `\w` and `\b`, which are
Unicode-aware in .NET; `RegexOptions.CultureInvariant` and `ExplicitCapture` on every pattern; the value of a pair is
the named group `v`; no `RegexOptions.NonBacktracking` (the patterns use lookarounds). Overlong scans are bounded: the END search of a key block stops at the next BEGIN, JWT segments are atomic, and the
placeholder check is a binary search, so a text full of BEGIN lines, token-like runs or placeholders stays linear
(`AdversarialInputTests`). A new rule needs a
corpus case in `tests/Aw.Redaction.Tests/Corpus.cs` (assembled from parts, so no scanner sees a whole token in the source) and,
when it is a new kind, a row above.

## AOT

`src/Redaction/Aw.Redaction.AotSmoke/run.sh` publishes a NativeAOT canary, fails on any IL2xxx/IL3xxx warning and
compares its output with `expected.txt`; CI's .NET job runs it. `Aw.Redaction.AotSmoke --stdin` scrubs standard input
(text to stdout, outcome and hits to stderr; exit 3 when blocked); `--extra <id>=<pattern>` (repeatable) and
`--timeout-ms <n>` add extra patterns, and a refused one exits 2 with each refusal on stderr. The canary's samples
include an accepted, a refused and a timing-out extra pattern.
