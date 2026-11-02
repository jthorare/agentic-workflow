using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>The profile's recognizers as the extractor and the as-of map cache see them (S-SK): which extra paths to read, what they find, and a hash that keys a cached extraction.</summary>
public sealed class RecognizerSet
{
    private const double Confidence = 0.8;
    private readonly List<Rule> _rules;

    private RecognizerSet(IReadOnlyList<ProfileRecognizer> recognizers)
    {
        _rules = [.. recognizers.Select(Compile).OfType<Rule>()];
        Hash = recognizers.Count == 0 ? "" : "r" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', recognizers.Select(r => $"{r.Kind}\0{r.Glob}\0{r.Pattern}\0{r.KeyFrom}")))))[..8];
    }

    /// <summary>No profile recognizers: the extractor's built-in table alone.</summary>
    public static RecognizerSet None { get; } = new([]);

    /// <summary>'' for none, else 'r' and 8 hex digits of the recognizers' content, so a changed set never reuses a cached extraction.</summary>
    public string Hash { get; }

    /// <summary>The set of these recognizers.</summary>
    /// <param name="recognizers">The checked entries.</param>
    /// <returns>The set.</returns>
    public static RecognizerSet From(IReadOnlyList<ProfileRecognizer> recognizers)
    {
        ArgumentNullException.ThrowIfNull(recognizers);
        return recognizers.Count == 0 ? None : new RecognizerSet(recognizers);
    }

    /// <summary>Whether a recognizer's glob matches the path, so the file is read even though the built-in table skips it. Deny globs are checked by the caller first.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>True when a recognizer reads the path.</returns>
    public bool Wants(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _rules.Exists(r => PathDenylist.Matches(path, r.Glob));
    }

    internal (List<(RepoFile File, Found Found)> Hits, bool TimedOut) Find(IEnumerable<RepoFile> files)
    {
        var hits = new List<(RepoFile, Found)>();
        var timedOut = false;
        foreach (var file in files.Where(f => f.Text.Length <= Recognizers.MaxFileLength).OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            foreach (var rule in _rules.Where(r => PathDenylist.Matches(file.Path, r.Glob)))
            {
                timedOut |= !rule.TryFind(file, hits);
            }
        }

        return (hits, timedOut);
    }

    private static Rule? Compile(ProfileRecognizer recognizer)
    {
        try
        {
            return new Rule(recognizer, LinearRegex.Create(recognizer.Pattern, RegexOptions.CultureInvariant));
        }
        catch (ArgumentException)
        {
            return null;   // the profile check refuses such an entry before a set is made; a hand-made one is just not run
        }
    }

    private sealed class Rule(ProfileRecognizer recognizer, Regex regex)
    {
        public string[] Glob { get; } = [recognizer.Glob];

        // False when the pattern ran out of time on the file; what it matched before is kept.
        public bool TryFind(RepoFile file, List<(RepoFile, Found)> hits)
        {
            try
            {
                foreach (Match match in regex.Matches(file.Text))
                {
                    var group = match.Groups[recognizer.KeyFrom];
                    if (group.Length > 0 && NodeKey.TryFrom($"surface:{recognizer.Kind}/{group.Value}", out var key) && NodeKinds.HasValidSurfaceKind(key))
                    {
                        hits.Add((file, new Found(key, group.Value, Confidence, Named: true, [])));
                    }
                }

                return true;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
