using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// What the reader has found so far in one file: every node's position by path (as written), every finding, and the
/// paths whose node was refused (an alias, a too-deep collection, a value that can't be read), which the later passes
/// skip so one problem is reported once.
/// </summary>
internal sealed class ReaderFindings(string file)
{
    private readonly List<RecipeFinding> _findings = [];
    private readonly HashSet<string> _refused = new(StringComparer.Ordinal);
    private readonly List<(JsonObject Parent, string Key)> _unknown = [];
    private readonly Dictionary<string, SourcePosition> _positions = new(StringComparer.Ordinal);

    public IReadOnlyList<RecipeFinding> All => _findings;

    public bool Any => _findings.Count > 0;

    public SourcePositions Positions => new(_positions);

    public void Record(string path, SourcePosition position) => _positions[path] = position;

    /// <summary>A finding at an explicit position (a duplicate key sits where the second one is written).</summary>
    public void At(string rule, string path, SourcePosition position, string message, string fix) =>
        _findings.Add(new RecipeFinding(rule, message, file, position.Line, position.Column, path, fix, FindingSeverity.Error));

    /// <summary>A finding located at the node at <paramref name="path"/>, or its nearest written ancestor.</summary>
    public void Add(string rule, string path, string message, string fix) => At(rule, path, Positions.Locate(path), message, fix);

    /// <summary>Marks the node at <paramref name="path"/> as reported: the later passes skip it and everything under it.</summary>
    public void Refuse(string path) => _refused.Add(path);

    /// <summary>The unknown fields reported, each as its mapping and key, so the reader can leave them out without parsing a path.</summary>
    public IReadOnlyList<(JsonObject Parent, string Key)> Unknown => _unknown;

    /// <summary>Marks the unknown field <paramref name="key"/> of <paramref name="parent"/>, at <paramref name="path"/>, as reported.</summary>
    public void RefuseUnknown(string path, JsonObject parent, string key)
    {
        Refuse(path);
        _unknown.Add((parent, key));
    }

    public bool IsRefused(string path) =>
        _refused.Any(r => path.Length >= r.Length && path.StartsWith(r, StringComparison.Ordinal)
            && (path.Length == r.Length || r.Length == 0 || path[r.Length] is '.' or '['));

    public static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;
}
