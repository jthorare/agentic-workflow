using System.Text;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Resolves a compose `image:` value to the repository and tag the lock pins.</summary>
public static class ComposeImageRef
{
    private const int MaxDepth = 8;

    /// <summary>Expands the value's ${NAME} and ${NAME:-default} references, drops a trailing @sha256 digest and splits the tag.</summary>
    /// <param name="raw">The `image:` text, such as ${ONYX_BACKEND_IMAGE:-onyxdotapp/onyx-backend:${IMAGE_TAG:-latest}}.</param>
    /// <param name="env">The values the compose file sees; a variable set and non-empty here wins over its default.</param>
    /// <returns>The normalized image, or SND-ONYX-004 for anything else: `:?` and `$$`, an unset variable without a default, or a value with no tag.</returns>
    public static Result<ImageRef> Resolve(string raw, IReadOnlyDictionary<string, string> env)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(env);
        var expanded = Expand(raw, env, depth: 0);
        if (expanded.IsError)
        {
            return expanded.Error;
        }

        return Split(expanded.Value, raw);
    }

    private static Result<string> Expand(string text, IReadOnlyDictionary<string, string> env, int depth)
    {
        if (depth > MaxDepth)
        {
            return Refused(text, "its ${…} references nest too deep");
        }

        var result = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] != '$')
            {
                _ = result.Append(text[i++]);
                continue;
            }

            var reference = Reference(text, i, env, depth);
            if (reference.IsError)
            {
                return reference.Error;
            }

            _ = result.Append(reference.Value.Text);
            i = reference.Value.End;
        }

        return result.ToString();
    }

    // text[start] is '$': only "${NAME}" and "${NAME:-default}" are understood; the default may hold nested references.
    private static Result<(string Text, int End)> Reference(string text, int start, IReadOnlyDictionary<string, string> env, int depth)
    {
        if (start + 1 >= text.Length || text[start + 1] != '{')
        {
            return Refused(text, "it uses a $ form other than ${NAME} and ${NAME:-default}");
        }

        var close = Close(text, start + 2);
        if (close < 0)
        {
            return Refused(text, "a ${ is never closed");
        }

        var body = text[(start + 2)..close];
        var colon = body.IndexOf(":-", StringComparison.Ordinal);
        var name = colon < 0 ? body : body[..colon];
        if (!IsName(name))
        {
            return Refused(text, $"it uses ${{{body}}}, which isn't ${{NAME}} or ${{NAME:-default}}");
        }

        if (env.TryGetValue(name, out var value) && value.Length > 0)
        {
            return (value, close + 1);
        }

        if (colon < 0)
        {
            return Refused(text, $"${{{name}}} has no value and no default");
        }

        var fallback = Expand(body[(colon + 2)..], env, depth + 1);
        return fallback.IsError ? fallback.Error : (fallback.Value, close + 1);
    }

    private static bool IsName(string name) => name.Length > 0 && name.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_');

    // The index of the '}' that closes the reference opened before `from`, counting nested ${ … }.
    private static int Close(string text, int from)
    {
        var depth = 1;
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '$' && i + 1 < text.Length && text[i + 1] == '{')
            {
                depth++;
                i++;
            }
            else if (text[i] == '}' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static Result<ImageRef> Split(string image, string raw)
    {
        var at = image.IndexOf('@', StringComparison.Ordinal);
        var name = at < 0 ? image : image[..at];
        var slash = name.LastIndexOf('/');
        var colon = name.LastIndexOf(':');
        if (colon <= slash || colon == name.Length - 1 || name.Length == 0)
        {
            return Refused(raw, "it has no tag");
        }

        var repository = name[..colon];
        return repository.Contains(' ', StringComparison.Ordinal) || name[(colon + 1)..].Contains('/', StringComparison.Ordinal)
            ? Refused(raw, "it isn't a repository and a tag")
            : new ImageRef(Normalize(repository), name[(colon + 1)..]);
    }

    private static string Normalize(string repository)
    {
        var slash = repository.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return "docker.io/library/" + repository;
        }

        var first = repository[..slash];
        return first.Contains('.', StringComparison.Ordinal) || first.Contains(':', StringComparison.Ordinal) || first == "localhost"
            ? repository
            : "docker.io/" + repository;
    }

    private static Error Refused(string raw, string why)
        => SindriErrors.OnyxPinsMalformed.With($"the compose image \"{raw}\" can't be resolved to a pinned image: {why}; the compose layout of the pinned release changed, so update context/onyx.lock and Sindri together");
}
