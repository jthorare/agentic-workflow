namespace Aw.Redaction;

/// <summary>
/// The token half of the secret-name rule. A name is split into tokens at every character that is not a letter or digit
/// (_, -, ., a space), at camelCase humps (apiTokens is api, tokens; APIKey is api, key) and where letters turn into
/// digits (password2 is password, 2; oauth2 is oauth, 2), lower-cased. It holds a secret when a token equals a secret word
/// (password, passwd, pwd, passphrase, credential, secret, token, apikey, accesskey, privatekey, secretkey, each also in the
/// plural) or when two neighbouring tokens spell one (api, access, private, secret, encryption, signing, hmac, master or
/// client, then key). Whole-token equality is the rule, so tokenizer, secretary and passwordless are ordinary.
/// The word token (a count in LLM and telemetry names) is ordinary only when a count word is directly before or after it
/// (max_token, max_tokens, input_tokens, token_count, tokens_used, totalTokens); a count word elsewhere in the name does not
/// help (used_refresh_tokens is secret). The other words have no count reading: password_min_length and password_changed_at
/// are secret.
/// </summary>
internal static class SecretNameTokens
{
    private static readonly HashSet<string> _words = new(StringComparer.Ordinal)
    {
        "password", "passwords", "passwd", "secret", "secrets", "apikey", "apikeys", "accesskey", "accesskeys",
        "privatekey", "privatekeys", "secretkey", "secretkeys", "passphrase", "passphrases", "credential", "credentials", "pwd",
    };

    private static readonly HashSet<string> _tokenWords = new(StringComparer.Ordinal) { "token", "tokens" };

    private static readonly HashSet<string> _keyPrefixes = new(StringComparer.Ordinal) { "api", "access", "private", "secret", "encryption", "signing", "hmac", "master", "client" };

    private static readonly HashSet<string> _keyWords = new(StringComparer.Ordinal) { "key", "keys" };

    private static readonly HashSet<string> _countWords = new(StringComparer.Ordinal)
    {
        "max", "min", "count", "counts", "total", "num", "number", "limit", "limits", "usage", "input", "output", "prompt",
        "completion", "cached", "reasoning", "budget", "remaining", "used", "size", "len", "length", "per",
    };

    /// <summary>True when a token of <paramref name="name"/> (or two neighbours) is a secret word.</summary>
    internal static bool Holds(string name)
    {
        var tokens = Split(name);
        for (var i = 0; i < tokens.Count; i++)
        {
            if (_words.Contains(tokens[i])
                || (_tokenWords.Contains(tokens[i]) && !CountBeside(tokens, i))
                || (i > 0 && _keyPrefixes.Contains(tokens[i - 1]) && _keyWords.Contains(tokens[i])))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the name ends in the word token with a count word directly before it (max_token, input_token): a count, so the pattern half of the rule (which matches any name ending in TOKEN) must not hide it.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True when the name is a counted token.</returns>
    internal static bool EndsInCountedToken(string name)
    {
        var tokens = Split(name);
        return tokens.Count > 1 && tokens[^1] == "token" && CountBeside(tokens, tokens.Count - 1);
    }

    private static bool CountBeside(List<string> tokens, int i) =>
        (i > 0 && _countWords.Contains(tokens[i - 1])) || (i + 1 < tokens.Count && _countWords.Contains(tokens[i + 1]));

    private static List<string> Split(string name)
    {
        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= name.Length; i++)
        {
            var c = i < name.Length ? name[i] : ' ';
            var alnum = char.IsAsciiLetterOrDigit(c);
            if (start >= 0 && (!alnum || StartsHump(name, i) || StartsDigits(name, i)))
            {
                tokens.Add(name[start..i].ToLowerInvariant());
                start = -1;
            }

            if (alnum && start < 0)
            {
                start = i;
            }
        }

        return tokens;
    }

    // Digits after a letter start a new token (password2).
    private static bool StartsDigits(string name, int i) =>
        i > 0 && i < name.Length && char.IsAsciiDigit(name[i]) && char.IsAsciiLetter(name[i - 1]);

    // A hump starts at an upper-case letter after a lower-case letter or digit (apiKey), or before the last upper-case letter of a run that is followed by lower case (APIKey).
    private static bool StartsHump(string name, int i)
    {
        if (i == 0 || i >= name.Length || !char.IsAsciiLetterUpper(name[i]))
        {
            return false;
        }

        var before = name[i - 1];
        return char.IsAsciiLetterLower(before) || char.IsAsciiDigit(before)
            || (char.IsAsciiLetterUpper(before) && i + 1 < name.Length && char.IsAsciiLetterLower(name[i + 1]));
    }
}
